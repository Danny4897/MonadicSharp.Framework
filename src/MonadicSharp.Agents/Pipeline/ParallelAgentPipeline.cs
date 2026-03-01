using MonadicSharp.Agents.Core;
using MonadicSharp.Agents.Errors;
using System.Diagnostics;

namespace MonadicSharp.Agents.Pipeline;

/// <summary>
/// Executes multiple agents in parallel over the same input and collects all results.
///
/// Two execution modes:
/// - <see cref="RunAllAsync"/> — returns all results (successes and failures) as a collection
/// - <see cref="RunAndMergeAsync"/> — merges successful results; returns failure if ANY agent fails
///
/// All agents run concurrently and share the same <see cref="AgentContext"/>.
/// Capability checks still apply per agent.
/// </summary>
public sealed class ParallelAgentPipeline<TInput, TOutput>
{
    private readonly string _name;
    private readonly IReadOnlyList<IAgent<TInput, TOutput>> _agents;

    public ParallelAgentPipeline(string name, IReadOnlyList<IAgent<TInput, TOutput>> agents)
    {
        _name = name;
        _agents = agents;
    }

    /// <summary>
    /// Runs all agents in parallel. Never short-circuits — always waits for all.
    /// Returns both successes and failures.
    /// </summary>
    public async Task<ParallelPipelineResult<TOutput>> RunAllAsync(
        TInput input,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        var tasks = _agents.Select(agent => ExecuteSingleAsync(agent, input, context, cancellationToken));
        var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);

        return new ParallelPipelineResult<TOutput>(_name, outcomes, startedAt, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Runs all agents in parallel. Returns failure if ANY agent fails.
    /// On success, calls <paramref name="merge"/> to combine all outputs into one.
    /// </summary>
    public async Task<Result<TMerged>> RunAndMergeAsync<TMerged>(
        TInput input,
        AgentContext context,
        Func<IReadOnlyList<TOutput>, Result<TMerged>> merge,
        CancellationToken cancellationToken = default)
    {
        var parallelResult = await RunAllAsync(input, context, cancellationToken).ConfigureAwait(false);

        if (parallelResult.HasFailures)
        {
            return Result<TMerged>.Failure(
                AgentError.ParallelExecutionFailed(
                    parallelResult.Failures.Select(f => (f.AgentName, f.Error!))));
        }

        return merge(parallelResult.Successes.Select(s => s.Value).ToList());
    }

    private static async Task<AgentOutcome<TOutput>> ExecuteSingleAsync(
        IAgent<TInput, TOutput> agent,
        TInput input,
        AgentContext context,
        CancellationToken cancellationToken)
    {
        var capCheck = context.Require(agent.RequiredCapabilities);
        if (capCheck.IsFailure)
        {
            var capErr = AgentError.CapabilityCheckFailed(agent.Name, agent.RequiredCapabilities, context.GrantedCapabilities);
            return new AgentOutcome<TOutput>(agent.Name, Result<TOutput>.Failure(capErr), TimeSpan.Zero);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await agent.ExecuteAsync(input, context, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            return new AgentOutcome<TOutput>(agent.Name, result, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new AgentOutcome<TOutput>(agent.Name, Result<TOutput>.Failure(AgentError.Cancelled(agent.Name)), sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var err = AgentError.UnhandledException(agent.Name, ex);
            return new AgentOutcome<TOutput>(agent.Name, Result<TOutput>.Failure(err), sw.Elapsed);
        }
    }
}

// ── Result types ──────────────────────────────────────────────────────────────

/// <summary>Outcome of a single agent in a parallel run.</summary>
public sealed record AgentOutcome<TOutput>(
    string AgentName,
    Result<TOutput> Result,
    TimeSpan Duration)
{
    public bool IsSuccess => Result.IsSuccess;
    public bool IsFailure => Result.IsFailure;
    public TOutput Value => Result.Value;
    public Error? Error => Result.IsFailure ? Result.Error : null;
}

/// <summary>Complete result of a parallel pipeline run.</summary>
public sealed class ParallelPipelineResult<TOutput>
{
    public string PipelineName { get; }
    public IReadOnlyList<AgentOutcome<TOutput>> Outcomes { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset CompletedAt { get; }
    public TimeSpan TotalDuration => CompletedAt - StartedAt;

    public IEnumerable<AgentOutcome<TOutput>> Successes => Outcomes.Where(o => o.IsSuccess);
    public IEnumerable<AgentOutcome<TOutput>> Failures => Outcomes.Where(o => o.IsFailure);
    public bool HasFailures => Outcomes.Any(o => o.IsFailure);
    public bool AllSucceeded => Outcomes.All(o => o.IsSuccess);

    internal ParallelPipelineResult(
        string name,
        IEnumerable<AgentOutcome<TOutput>> outcomes,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        PipelineName = name;
        Outcomes = outcomes.ToList();
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    public override string ToString()
        => $"ParallelPipeline[{PipelineName}]: {Outcomes.Count(o => o.IsSuccess)}/{Outcomes.Count} succeeded in {TotalDuration.TotalMilliseconds:F0}ms";
}

/// <summary>Static factory for parallel pipelines.</summary>
public static class ParallelAgentPipeline
{
    /// <summary>Creates a parallel pipeline from a set of agents that share input/output types.</summary>
    public static ParallelAgentPipeline<TInput, TOutput> Of<TInput, TOutput>(
        string name,
        params IAgent<TInput, TOutput>[] agents)
        => new(name, agents);
}
