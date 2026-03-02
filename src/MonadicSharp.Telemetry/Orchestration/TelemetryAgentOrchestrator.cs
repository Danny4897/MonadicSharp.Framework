using MonadicSharp.Agents;
using MonadicSharp.Agents.Core;
using MonadicSharp.Agents.Orchestration;
using MonadicSharp.Agents.Resilience;
using MonadicSharp.Telemetry.Core;
using MonadicSharp.Telemetry.Middleware;
using Microsoft.Extensions.Logging;

namespace MonadicSharp.Telemetry.Orchestration;

/// <summary>
/// An <see cref="AgentOrchestrator"/> decorator that emits OpenTelemetry metrics
/// and distributed traces for every agent dispatch.
///
/// Wraps the inner orchestrator via delegation — all registration and dispatch
/// semantics are unchanged; telemetry is additive and opt-in.
///
/// Usage:
/// <code>
/// var orchestrator = new TelemetryAgentOrchestrator(
///     new AgentOrchestrator(),
///     AgentMeter.Instance);
///
/// orchestrator
///     .Register(summaryAgent)
///     .Register(classifierAgent);
///
/// var result = await orchestrator.DispatchAsync&lt;string, Summary&gt;(
///     "SummaryAgent", input, context);
/// </code>
/// </summary>
public sealed class TelemetryAgentOrchestrator
{
    private readonly AgentOrchestrator _inner;
    private readonly AgentMeter _meter;

    /// <summary>Exposes the inner orchestrator's audit log.</summary>
    public IReadOnlyList<OrchestratorAuditEntry> AuditLog => _inner.AuditLog;

    /// <summary>Names of all registered agents.</summary>
    public IEnumerable<string> RegisteredAgents => _inner.RegisteredAgents;

    public TelemetryAgentOrchestrator(AgentOrchestrator inner, AgentMeter? meter = null)
    {
        _inner = inner;
        _meter = meter ?? AgentMeter.Instance;
    }

    // ── Registration ─────────────────────────────────────────────────────────

    /// <summary>
    /// Registers an agent, wrapping it transparently with a
    /// <see cref="TelemetryAgentWrapper{TInput,TOutput}"/> so every dispatch
    /// is automatically instrumented.
    /// </summary>
    public TelemetryAgentOrchestrator Register<TInput, TOutput>(IAgent<TInput, TOutput> agent)
    {
        _inner.Register(new TelemetryAgentWrapper<TInput, TOutput>(agent, _meter));
        return this;
    }

    // ── Dispatch ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches to a named agent.
    /// In addition to the inner orchestrator's behaviour, emits:
    /// - An Activity span ("agent.execute" via TelemetryAgentWrapper)
    /// - Pipeline-level metrics are handled by <c>TelemetryPipelineExtensions</c>
    /// </summary>
    public Task<Result<TOutput>> DispatchAsync<TInput, TOutput>(
        string agentName,
        TInput input,
        AgentContext context,
        CancellationToken cancellationToken = default)
        => _inner.DispatchAsync<TInput, TOutput>(agentName, input, context, cancellationToken);

    // ── Circuit breaker ───────────────────────────────────────────────────────

    /// <summary>Returns the circuit breaker state for a registered agent.</summary>
    public Result<CircuitState> GetCircuitState(string agentName)
        => _inner.GetCircuitState(agentName);
}
