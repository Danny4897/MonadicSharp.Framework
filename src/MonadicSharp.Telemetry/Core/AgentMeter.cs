using System.Diagnostics.Metrics;

namespace MonadicSharp.Telemetry.Core;

/// <summary>
/// Central registry of all MonadicSharp metrics instruments.
///
/// Built on <see cref="System.Diagnostics.Metrics.Meter"/> — the .NET 8 standard
/// that OpenTelemetry SDK reads natively via <c>AddMeter("MonadicSharp.Agents")</c>.
///
/// All instruments are created once and shared. Consumers should call
/// <see cref="Instance"/> to obtain the singleton.
///
/// Thread-safety: all <see cref="Meter"/> instruments are thread-safe by design.
/// </summary>
public sealed class AgentMeter : IDisposable
{
    private readonly Meter _meter;

    // ── Singleton ─────────────────────────────────────────────────────────────

    /// <summary>Global singleton. Use this unless you need isolated meters for testing.</summary>
    public static readonly AgentMeter Instance = new(TelemetryConstants.MeterName);

    // ── Instruments ───────────────────────────────────────────────────────────

    /// <summary>
    /// Histogram: per-agent execution duration in milliseconds.
    /// Tags: agent.name, agent.status, session.id
    /// </summary>
    public Histogram<double> AgentExecutionDuration { get; }

    /// <summary>
    /// Counter: total agent executions.
    /// Tags: agent.name, agent.status
    /// </summary>
    public Counter<long> AgentExecutionsTotal { get; }

    /// <summary>
    /// Counter: capability check failures.
    /// Tags: agent.name
    /// </summary>
    public Counter<long> AgentCapabilityFailures { get; }

    /// <summary>
    /// Histogram: full pipeline execution duration in milliseconds.
    /// Tags: pipeline.name, pipeline.status
    /// </summary>
    public Histogram<double> PipelineExecutionDuration { get; }

    /// <summary>
    /// Counter: total pipeline runs.
    /// Tags: pipeline.name, pipeline.status
    /// </summary>
    public Counter<long> PipelineExecutionsTotal { get; }

    /// <summary>
    /// Histogram: number of completed steps in a pipeline run.
    /// Tags: pipeline.name, pipeline.status
    /// </summary>
    public Histogram<int> PipelineStepsCompleted { get; }

    /// <summary>
    /// Counter: circuit breaker state transitions.
    /// Tags: agent.name, circuit_breaker.state (target state)
    /// </summary>
    public Counter<long> CircuitBreakerTransitions { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="AgentMeter"/> with the given meter name.
    /// In production use <see cref="Instance"/>; pass a unique name in tests.
    /// </summary>
    public AgentMeter(string meterName)
    {
        _meter = new Meter(meterName, "1.0.0");

        AgentExecutionDuration = _meter.CreateHistogram<double>(
            TelemetryConstants.AgentExecutionDuration,
            unit: "ms",
            description: "Duration of individual agent executions in milliseconds.");

        AgentExecutionsTotal = _meter.CreateCounter<long>(
            TelemetryConstants.AgentExecutionsTotal,
            description: "Total number of agent executions, labelled by outcome.");

        AgentCapabilityFailures = _meter.CreateCounter<long>(
            TelemetryConstants.AgentCapabilityFailures,
            description: "Number of times an agent was rejected due to insufficient capabilities.");

        PipelineExecutionDuration = _meter.CreateHistogram<double>(
            TelemetryConstants.PipelineExecutionDuration,
            unit: "ms",
            description: "Duration of full pipeline runs in milliseconds.");

        PipelineExecutionsTotal = _meter.CreateCounter<long>(
            TelemetryConstants.PipelineExecutionsTotal,
            description: "Total number of pipeline runs, labelled by outcome.");

        PipelineStepsCompleted = _meter.CreateHistogram<int>(
            TelemetryConstants.PipelineStepsCompleted,
            description: "Number of steps completed in a pipeline run.");

        CircuitBreakerTransitions = _meter.CreateCounter<long>(
            TelemetryConstants.CircuitBreakerTransitions,
            description: "Number of circuit breaker state transitions.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Records a completed agent execution.
    /// </summary>
    public void RecordAgentExecution(string agentName, bool success, double durationMs, string sessionId)
    {
        var status = success ? TelemetryConstants.StatusSuccess : TelemetryConstants.StatusFailure;

        AgentExecutionDuration.Record(
            durationMs,
            new KeyValuePair<string, object?>(TelemetryConstants.AttrAgentName, agentName),
            new KeyValuePair<string, object?>(TelemetryConstants.AttrAgentStatus, status),
            new KeyValuePair<string, object?>(TelemetryConstants.AttrSessionId, sessionId));

        AgentExecutionsTotal.Add(
            1,
            new KeyValuePair<string, object?>(TelemetryConstants.AttrAgentName, agentName),
            new KeyValuePair<string, object?>(TelemetryConstants.AttrAgentStatus, status));
    }

    /// <summary>
    /// Records a capability check failure for an agent.
    /// </summary>
    public void RecordCapabilityFailure(string agentName)
    {
        AgentCapabilityFailures.Add(
            1,
            new KeyValuePair<string, object?>(TelemetryConstants.AttrAgentName, agentName));
    }

    /// <summary>
    /// Records a completed pipeline run.
    /// </summary>
    public void RecordPipelineExecution(string pipelineName, bool success, double durationMs, int stepCount)
    {
        var status = success ? TelemetryConstants.StatusSuccess : TelemetryConstants.StatusFailure;

        PipelineExecutionDuration.Record(
            durationMs,
            new KeyValuePair<string, object?>(TelemetryConstants.AttrPipelineName, pipelineName),
            new KeyValuePair<string, object?>(TelemetryConstants.AttrPipelineStatus, status));

        PipelineExecutionsTotal.Add(
            1,
            new KeyValuePair<string, object?>(TelemetryConstants.AttrPipelineName, pipelineName),
            new KeyValuePair<string, object?>(TelemetryConstants.AttrPipelineStatus, status));

        PipelineStepsCompleted.Record(
            stepCount,
            new KeyValuePair<string, object?>(TelemetryConstants.AttrPipelineName, pipelineName),
            new KeyValuePair<string, object?>(TelemetryConstants.AttrPipelineStatus, status));
    }

    /// <summary>
    /// Records a circuit breaker state transition.
    /// </summary>
    public void RecordCircuitBreakerTransition(string agentName, string targetState)
    {
        CircuitBreakerTransitions.Add(
            1,
            new KeyValuePair<string, object?>(TelemetryConstants.AttrAgentName, agentName),
            new KeyValuePair<string, object?>(TelemetryConstants.AttrCircuitState, targetState));
    }

    public void Dispose() => _meter.Dispose();
}
