using MonadicSharp.Agents.Errors;

namespace MonadicSharp.Agents.Core;

/// <summary>
/// Immutable execution context passed to every agent invocation.
///
/// The context carries:
/// - A unique session ID for correlating logs and audit events
/// - The set of capabilities granted to this execution scope
/// - Arbitrary key/value metadata (tenant ID, correlation ID, user identity, etc.)
/// - A cancellation token that propagates timeout and shutdown signals
///
/// Contexts are immutable by design. To restrict capabilities for a child agent,
/// call <see cref="Narrow"/> — it always returns a context with fewer or equal
/// permissions, never more.
/// </summary>
public sealed class AgentContext
{
    private readonly Dictionary<string, object> _metadata;

    /// <summary>Unique identifier for the top-level session (shared across all agents in a run).</summary>
    public string SessionId { get; }

    /// <summary>The capabilities granted to agents running in this context.</summary>
    public AgentCapability GrantedCapabilities { get; }

    /// <summary>Read-only view of contextual metadata.</summary>
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    /// <summary>Propagated cancellation token.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>When this context was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    private AgentContext(
        string sessionId,
        AgentCapability grantedCapabilities,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken)
    {
        SessionId = sessionId;
        GrantedCapabilities = grantedCapabilities;
        _metadata = metadata;
        CancellationToken = cancellationToken;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>Creates a root context with the specified capabilities.</summary>
    public static AgentContext Create(
        AgentCapability capabilities,
        CancellationToken cancellationToken = default,
        string? sessionId = null)
        => new(
            sessionId ?? Guid.NewGuid().ToString("N"),
            capabilities,
            new Dictionary<string, object>(),
            cancellationToken);

    /// <summary>Creates a fully-trusted context. Use only in internal/test scenarios.</summary>
    public static AgentContext Trusted(CancellationToken cancellationToken = default)
        => Create(AgentCapability.All, cancellationToken);

    /// <summary>Creates a sandboxed context with no capabilities.</summary>
    public static AgentContext Sandboxed(CancellationToken cancellationToken = default)
        => Create(AgentCapability.None, cancellationToken);

    // ── Capability checks ─────────────────────────────────────────────────────

    /// <summary>Returns true if this context grants all the specified capabilities.</summary>
    public bool HasCapability(AgentCapability capability)
        => GrantedCapabilities.Satisfies(capability);

    /// <summary>
    /// Verifies that this context grants the required capabilities.
    /// Returns a failure with a typed <see cref="AgentError"/> if any capability is missing.
    /// </summary>
    public Result<AgentContext> Require(AgentCapability required)
    {
        var missing = GrantedCapabilities.Missing(required);
        return missing == AgentCapability.None
            ? Result<AgentContext>.Success(this)
            : Result<AgentContext>.Failure(AgentError.InsufficientCapabilities(required, missing));
    }

    // ── Narrowing ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a new context that is a strict subset of this one.
    /// The result can never grant more capabilities than the current context.
    /// This is the correct way to create a child-agent context.
    /// </summary>
    public AgentContext Narrow(AgentCapability allowedSubset)
        => new(
            SessionId,
            GrantedCapabilities & allowedSubset,
            new Dictionary<string, object>(_metadata),
            CancellationToken);

    // ── Metadata ──────────────────────────────────────────────────────────────

    /// <summary>Returns a new context with an additional metadata entry.</summary>
    public AgentContext WithMetadata(string key, object value)
    {
        var meta = new Dictionary<string, object>(_metadata) { [key] = value };
        return new AgentContext(SessionId, GrantedCapabilities, meta, CancellationToken);
    }

    /// <summary>Retrieves a strongly-typed metadata value, or returns a failure.</summary>
    public Result<T> GetMetadata<T>(string key)
    {
        if (!_metadata.TryGetValue(key, out var value))
            return Result<T>.Failure(AgentError.MetadataKeyNotFound(key));

        if (value is not T typed)
            return Result<T>.Failure(AgentError.MetadataTypeMismatch(key, typeof(T), value.GetType()));

        return Result<T>.Success(typed);
    }

    public override string ToString()
        => $"AgentContext[{SessionId[..8]}] Capabilities={GrantedCapabilities}";
}
