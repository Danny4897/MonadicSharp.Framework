namespace MonadicSharp.Telemetry.Core;

/// <summary>
/// Canonical names for all MonadicSharp telemetry instruments and attributes.
/// Follows OpenTelemetry semantic conventions where applicable.
/// </summary>
public static class TelemetryConstants
{
    // ── Source / Meter names ──────────────────────────────────────────────────

    /// <summary>Name of the <see cref="System.Diagnostics.ActivitySource"/> used for all agent spans.</summary>
    public const string ActivitySourceName = "MonadicSharp.Agents";

    /// <summary>Name of the <see cref="System.Diagnostics.Metrics.Meter"/> used for all agent metrics.</summary>
    public const string MeterName = "MonadicSharp.Agents";

    // ── Metric instrument names ───────────────────────────────────────────────

    /// <summary>Histogram: agent execution duration in milliseconds.</summary>
    public const string AgentExecutionDuration = "agent.execution.duration";

    /// <summary>Counter: total agent executions (tagged with agent.name and agent.status).</summary>
    public const string AgentExecutionsTotal = "agent.executions.total";

    /// <summary>Counter: number of capability check failures.</summary>
    public const string AgentCapabilityFailures = "agent.capability.checks.failed";

    /// <summary>Histogram: full pipeline execution duration in milliseconds.</summary>
    public const string PipelineExecutionDuration = "pipeline.execution.duration";

    /// <summary>Counter: total pipeline runs (tagged with pipeline.name and pipeline.status).</summary>
    public const string PipelineExecutionsTotal = "pipeline.executions.total";

    /// <summary>Histogram: number of steps completed in a pipeline run.</summary>
    public const string PipelineStepsCompleted = "pipeline.steps.completed";

    /// <summary>Counter: circuit breaker state transitions.</summary>
    public const string CircuitBreakerTransitions = "agent.circuit_breaker.transitions";

    // ── Attribute / tag keys ─────────────────────────────────────────────────

    /// <summary>The human-readable name of the agent.</summary>
    public const string AttrAgentName = "agent.name";

    /// <summary>Execution outcome: "success" or "failure".</summary>
    public const string AttrAgentStatus = "agent.status";

    /// <summary>Error code when the agent fails (e.g. "capability.insufficient").</summary>
    public const string AttrErrorType = "error.type";

    /// <summary>The name of the pipeline.</summary>
    public const string AttrPipelineName = "pipeline.name";

    /// <summary>Pipeline outcome: "success" or "failure".</summary>
    public const string AttrPipelineStatus = "pipeline.status";

    /// <summary>Circuit breaker target state: "open", "half_open", "closed".</summary>
    public const string AttrCircuitState = "circuit_breaker.state";

    /// <summary>The session ID from <see cref="MonadicSharp.Agents.Core.AgentContext"/>.</summary>
    public const string AttrSessionId = "session.id";

    // ── Status values ─────────────────────────────────────────────────────────

    public const string StatusSuccess = "success";
    public const string StatusFailure = "failure";
}
