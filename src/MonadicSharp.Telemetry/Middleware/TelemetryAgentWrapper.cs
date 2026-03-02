using System.Diagnostics;
using MonadicSharp.Agents;
using MonadicSharp.Agents.Core;
using MonadicSharp.Telemetry.Core;

namespace MonadicSharp.Telemetry.Middleware;

/// <summary>
/// Decorator that wraps any <see cref="IAgent{TInput,TOutput}"/> with OpenTelemetry
/// instrumentation: one Activity span and metric recording per execution.
///
/// Usage:
/// <code>
/// IAgent&lt;string, Summary&gt; instrumented =
///     new TelemetryAgentWrapper&lt;string, Summary&gt;(summaryAgent, AgentMeter.Instance);
/// </code>
///
/// The decorator is transparent — it forwards <see cref="Name"/> and
/// <see cref="RequiredCapabilities"/> unchanged so the orchestrator sees the same contract.
/// </summary>
public sealed class TelemetryAgentWrapper<TInput, TOutput> : IAgent<TInput, TOutput>
{
    private readonly IAgent<TInput, TOutput> _inner;
    private readonly AgentMeter _meter;

    public string Name => _inner.Name;
    public AgentCapability RequiredCapabilities => _inner.RequiredCapabilities;

    public TelemetryAgentWrapper(IAgent<TInput, TOutput> inner, AgentMeter? meter = null)
    {
        _inner = inner;
        _meter = meter ?? AgentMeter.Instance;
    }

    public async Task<Result<TOutput>> ExecuteAsync(
        TInput input,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = AgentActivitySource.StartAgentActivity(_inner.Name, context);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await _inner.ExecuteAsync(input, context, cancellationToken)
                .ConfigureAwait(false);

            sw.Stop();
            var success = result.IsSuccess;
            var errorCode = result.IsFailure ? result.Error.Code : null;

            _meter.RecordAgentExecution(_inner.Name, success, sw.Elapsed.TotalMilliseconds, context.SessionId);
            AgentActivitySource.CompleteAgentActivity(activity, success, errorCode);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _meter.RecordAgentExecution(_inner.Name, false, sw.Elapsed.TotalMilliseconds, context.SessionId);
            AgentActivitySource.CompleteAgentActivity(activity, false, ex.GetType().Name);
            throw;
        }
    }
}
