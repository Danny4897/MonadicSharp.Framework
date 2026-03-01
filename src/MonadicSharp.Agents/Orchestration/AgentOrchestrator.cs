using MonadicSharp.Agents.Core;
using MonadicSharp.Agents.Errors;
using MonadicSharp.Agents.Pipeline;
using MonadicSharp.Agents.Resilience;
using Microsoft.Extensions.Logging;

namespace MonadicSharp.Agents.Orchestration;

/// <summary>
/// Central coordinator for multi-agent systems.
///
/// The orchestrator:
/// 1. Maintains a registry of named agents
/// 2. Validates capability requirements before dispatching
/// 3. Wraps agent calls with a <see cref="CircuitBreaker"/> per agent
/// 4. Records all dispatches in an immutable <see cref="OrchestratorAuditLog"/>
/// 5. Never throws — all errors return as typed <see cref="Result{T}"/> values
///
/// This is the entry point for building multi-agent workflows where agents
/// need to be discovered by name and coordinated dynamically.
/// </summary>
public sealed class AgentOrchestrator
{
    private readonly Dictionary<string, object> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CircuitBreaker> _breakers = new(StringComparer.OrdinalIgnoreCase);
    private readonly OrchestratorAuditLog _auditLog = new();
    private readonly ILogger<AgentOrchestrator>? _logger;

    private readonly int _circuitBreakerThreshold;
    private readonly TimeSpan _circuitOpenDuration;

    public IReadOnlyList<OrchestratorAuditEntry> AuditLog => _auditLog.Entries;

    public AgentOrchestrator(
        ILogger<AgentOrchestrator>? logger = null,
        int circuitBreakerThreshold = 5,
        TimeSpan? circuitOpenDuration = null)
    {
        _logger = logger;
        _circuitBreakerThreshold = circuitBreakerThreshold;
        _circuitOpenDuration = circuitOpenDuration ?? TimeSpan.FromSeconds(30);
    }

    // ── Registration ─────────────────────────────────────────────────────────

    /// <summary>Registers an agent under its <see cref="IAgent{TIn,TOut}.Name"/>.</summary>
    public AgentOrchestrator Register<TInput, TOutput>(IAgent<TInput, TOutput> agent)
    {
        _agents[agent.Name] = agent;
        _breakers[agent.Name] = new CircuitBreaker(agent.Name, _circuitBreakerThreshold, _circuitOpenDuration);
        _logger?.LogInformation("Registered agent '{AgentName}' requiring [{Capabilities}]",
            agent.Name, string.Join(", ", agent.RequiredCapabilities.ToNames()));
        return this;
    }

    /// <summary>Returns names of all registered agents.</summary>
    public IEnumerable<string> RegisteredAgents => _agents.Keys;

    // ── Dispatch ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches a call to a named agent.
    /// Verifies the agent exists, checks capabilities, applies the circuit breaker,
    /// and records the dispatch in the audit log.
    /// </summary>
    public async Task<Result<TOutput>> DispatchAsync<TInput, TOutput>(
        string agentName,
        TInput input,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_agents.TryGetValue(agentName, out var agentObj))
        {
            var err = AgentError.AgentNotFound(agentName);
            _auditLog.Record(agentName, false, err, context.SessionId);
            return Result<TOutput>.Failure(err);
        }

        if (agentObj is not IAgent<TInput, TOutput> agent)
        {
            var err = AgentError.TypeMismatch(agentName, typeof(TInput), input!.GetType());
            _auditLog.Record(agentName, false, err, context.SessionId);
            return Result<TOutput>.Failure(err);
        }

        var breaker = _breakers[agentName];

        var result = await breaker.ExecuteAsync(async ct =>
        {
            var capCheck = context.Require(agent.RequiredCapabilities);
            if (capCheck.IsFailure)
                return Result<TOutput>.Failure(AgentError.CapabilityCheckFailed(agentName, agent.RequiredCapabilities, context.GrantedCapabilities));

            return await agent.ExecuteAsync(input, context, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        _auditLog.Record(agentName, result.IsSuccess, result.IsFailure ? result.Error : null, context.SessionId);

        if (result.IsSuccess)
            _logger?.LogInformation("Agent '{AgentName}' succeeded [session={SessionId}]", agentName, context.SessionId[..8]);
        else
            _logger?.LogWarning("Agent '{AgentName}' failed: {Error} [session={SessionId}]", agentName, result.Error.Message, context.SessionId[..8]);

        return result;
    }

    /// <summary>Returns circuit breaker state for a registered agent.</summary>
    public Result<CircuitState> GetCircuitState(string agentName)
        => _breakers.TryGetValue(agentName, out var cb)
            ? Result<CircuitState>.Success(cb.State)
            : Result<CircuitState>.Failure(AgentError.AgentNotFound(agentName));
}

// ── Audit log ─────────────────────────────────────────────────────────────────

/// <summary>Immutable record of a single agent dispatch.</summary>
public sealed record OrchestratorAuditEntry(
    string AgentName,
    bool Succeeded,
    Error? Error,
    string SessionId,
    DateTimeOffset Timestamp);

/// <summary>Thread-safe, append-only audit log for the orchestrator.</summary>
internal sealed class OrchestratorAuditLog
{
    private readonly List<OrchestratorAuditEntry> _entries = [];
    private readonly object _lock = new();

    public IReadOnlyList<OrchestratorAuditEntry> Entries
    {
        get { lock (_lock) return _entries.ToList(); }
    }

    public void Record(string agentName, bool succeeded, Error? error, string sessionId)
    {
        lock (_lock)
        {
            _entries.Add(new OrchestratorAuditEntry(agentName, succeeded, error, sessionId, DateTimeOffset.UtcNow));
        }
    }
}
