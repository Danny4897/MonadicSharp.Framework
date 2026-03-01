using MonadicSharp.Agents.Core;
using MonadicSharp.Agents.Errors;
using System.Diagnostics;

namespace MonadicSharp.Agents.Pipeline;

/// <summary>
/// A typed, sequential agent pipeline that threads data from one agent to the next
/// using Railway-Oriented Programming.
///
/// Key properties:
/// - Type-safe: TInput and TOutput are enforced at compile time per step
/// - Short-circuits on the first failure — subsequent agents do not run
/// - Checks capabilities before each agent executes
/// - Produces a full <see cref="PipelineTrace"/> even when a step fails
///
/// Usage:
/// <code>
/// var result = await AgentPipeline
///     .Start&lt;string&gt;("DocumentProcessing")
///     .Then(extractorAgent)
///     .Then(summaryAgent)
///     .Then(classifierAgent)
///     .RunAsync(rawText, context);
/// </code>
/// </summary>
public sealed class AgentPipeline<TInput, TOutput>
{
    private readonly string _name;
    private readonly Func<TInput, AgentContext, CancellationToken, Task<(Result<TOutput>, PipelineStepTrace)>> _executor;

    internal AgentPipeline(string name, Func<TInput, AgentContext, CancellationToken, Task<(Result<TOutput>, PipelineStepTrace)>> executor)
    {
        _name = name;
        _executor = executor;
    }

    /// <summary>
    /// Chains another agent after this pipeline.
    /// The output of this pipeline becomes the input of <paramref name="next"/>.
    /// </summary>
    public AgentPipeline<TInput, TNext> Then<TNext>(IAgent<TOutput, TNext> next)
        => new(
            _name,
            async (input, ctx, ct) =>
            {
                var (current, trace) = await _executor(input, ctx, ct).ConfigureAwait(false);

                if (current.IsFailure)
                    return (Result<TNext>.Failure(current.Error), trace);

                // Capability guard
                var capCheck = ctx.Require(next.RequiredCapabilities);
                if (capCheck.IsFailure)
                {
                    var capError = AgentError.CapabilityCheckFailed(next.Name, next.RequiredCapabilities, ctx.GrantedCapabilities);
                    var capStep = new PipelineStepTrace(next.Name, TimeSpan.Zero, false, capError);
                    return (Result<TNext>.Failure(capError), trace.Append(capStep));
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    var result = await next.ExecuteAsync(current.Value, ctx, ct).ConfigureAwait(false);
                    sw.Stop();
                    var step = new PipelineStepTrace(next.Name, sw.Elapsed, result.IsSuccess, result.IsFailure ? result.Error : null);

                    if (result.IsFailure)
                    {
                        var wrapped = AgentError.PipelineStepFailed(_name, next.Name, result.Error);
                        return (Result<TNext>.Failure(wrapped), trace.Append(step));
                    }

                    return (result, trace.Append(step));
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    var err = AgentError.Cancelled(next.Name);
                    var step = new PipelineStepTrace(next.Name, sw.Elapsed, false, err);
                    return (Result<TNext>.Failure(err), trace.Append(step));
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    var err = AgentError.UnhandledException(next.Name, ex);
                    var step = new PipelineStepTrace(next.Name, sw.Elapsed, false, err);
                    return (Result<TNext>.Failure(err), trace.Append(step));
                }
            });

    /// <summary>
    /// Executes the pipeline, returning both the result and the full execution trace.
    /// The trace is always populated up to the point of failure.
    /// </summary>
    public async Task<PipelineResult<TOutput>> RunAsync(TInput input, AgentContext context, CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var (result, trace) = await _executor(input, context, cancellationToken).ConfigureAwait(false);
        return new PipelineResult<TOutput>(_name, result, trace, started, DateTimeOffset.UtcNow);
    }
}

/// <summary>Entry point for building typed pipelines.</summary>
public static class AgentPipeline
{
    /// <summary>
    /// Starts a new named pipeline with a single initial agent.
    /// The pipeline name appears in all traces and audit logs.
    /// </summary>
    public static AgentPipeline<TInput, TOutput> Start<TInput, TOutput>(
        string name,
        IAgent<TInput, TOutput> first)
        => new(
            name,
            async (input, ctx, ct) =>
            {
                var capCheck = ctx.Require(first.RequiredCapabilities);
                if (capCheck.IsFailure)
                {
                    var capError = AgentError.CapabilityCheckFailed(first.Name, first.RequiredCapabilities, ctx.GrantedCapabilities);
                    var capStep = new PipelineStepTrace(first.Name, TimeSpan.Zero, false, capError);
                    return (Result<TOutput>.Failure(capError), PipelineStepTrace.Empty.Append(capStep));
                }

                var sw = Stopwatch.StartNew();
                try
                {
                    var result = await first.ExecuteAsync(input, ctx, ct).ConfigureAwait(false);
                    sw.Stop();
                    var step = new PipelineStepTrace(first.Name, sw.Elapsed, result.IsSuccess, result.IsFailure ? result.Error : null);
                    return (result, PipelineStepTrace.Empty.Append(step));
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    var err = AgentError.Cancelled(first.Name);
                    var step = new PipelineStepTrace(first.Name, sw.Elapsed, false, err);
                    return (Result<TOutput>.Failure(err), PipelineStepTrace.Empty.Append(step));
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    var err = AgentError.UnhandledException(first.Name, ex);
                    var step = new PipelineStepTrace(first.Name, sw.Elapsed, false, err);
                    return (Result<TOutput>.Failure(err), PipelineStepTrace.Empty.Append(step));
                }
            });
}

// ── Trace types ──────────────────────────────────────────────────────────────

/// <summary>Immutable record of a single step within a pipeline execution.</summary>
public sealed record PipelineStepTrace
{
    public static readonly PipelineStepTrace Empty = new("__empty__", TimeSpan.Zero, true, null);

    private readonly List<PipelineStepTrace> _steps;

    public string AgentName { get; }
    public TimeSpan Duration { get; }
    public bool Succeeded { get; }
    public Error? Error { get; }

    // Internal: accumulate steps as the pipeline progresses
    internal IReadOnlyList<PipelineStepTrace> AllSteps => _steps;

    internal PipelineStepTrace(string agentName, TimeSpan duration, bool succeeded, Error? error)
    {
        AgentName = agentName;
        Duration = duration;
        Succeeded = succeeded;
        Error = error;
        _steps = [];
    }

    private PipelineStepTrace(List<PipelineStepTrace> steps) : this("__root__", TimeSpan.Zero, true, null)
    {
        _steps = steps;
    }

    internal PipelineStepTrace Append(PipelineStepTrace step)
    {
        var newSteps = new List<PipelineStepTrace>(_steps);
        // Only add self if this is a real step (not an accumulator or empty placeholder)
        if (AgentName != "__empty__" && AgentName != "__root__") newSteps.Add(this);
        newSteps.Add(step);
        return new PipelineStepTrace(newSteps);
    }
}

/// <summary>
/// The complete result of a pipeline execution: the outcome + full trace.
/// Even on failure, the trace contains all steps up to the error.
/// </summary>
public sealed class PipelineResult<TOutput>
{
    public string PipelineName { get; }
    public Result<TOutput> Result { get; }
    public IReadOnlyList<PipelineStepTrace> Steps { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset CompletedAt { get; }
    public TimeSpan TotalDuration => CompletedAt - StartedAt;

    public bool IsSuccess => Result.IsSuccess;
    public bool IsFailure => Result.IsFailure;
    public TOutput Value => Result.Value;
    public Error Error => Result.Error;

    internal PipelineResult(
        string pipelineName,
        Result<TOutput> result,
        PipelineStepTrace trace,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        PipelineName = pipelineName;
        Result = result;
        Steps = trace.AllSteps.Count > 0 ? trace.AllSteps : (trace.AgentName != "__empty__" ? [trace] : []);
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    public override string ToString()
        => $"Pipeline[{PipelineName}] {(IsSuccess ? "OK" : "FAILED")} — {Steps.Count} steps, {TotalDuration.TotalMilliseconds:F0}ms";
}
