using MonadicSharp.Agents.Core;
using MonadicSharp.Security.Errors;
using MonadicSharp.Security.Masking;

namespace MonadicSharp.Security.Audit;

/// <summary>
/// Severity level of an audit event.
/// </summary>
public enum AuditSeverity { Info, Warning, Security, Critical }

/// <summary>
/// Immutable record of a single auditable event in an agent session.
/// All sensitive fields are masked before recording.
/// </summary>
public sealed record AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string SessionId { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public AuditSeverity Severity { get; init; } = AuditSeverity.Info;
    public bool Succeeded { get; init; }
    public Error? SecurityError { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>Produces a single-line structured log representation.</summary>
    public override string ToString()
        => $"[{Timestamp:HH:mm:ss.fff}] [{Severity}] {EventType} | agent={AgentName} session={SessionId[..Math.Min(8, SessionId.Length)]} ok={Succeeded}"
           + (SecurityError != null ? $" | security_error={SecurityError.Code}" : "");
}

/// <summary>
/// Thread-safe, append-only audit trail for agent sessions.
///
/// The AuditTrail automatically:
/// - Masks secrets in all recorded content (via <see cref="SecretMasker"/>)
/// - Attaches session correlation from <see cref="AgentContext"/>
/// - Exposes typed queries for security analysis (e.g., all security events, failures only)
///
/// Design rule: once written, events are immutable and cannot be deleted.
/// This is a deliberate security property — audit trails must be tamper-evident.
/// </summary>
public sealed class AuditTrail
{
    private readonly List<AuditEvent> _events = [];
    private readonly object _lock = new();
    private readonly SecretMasker _masker;

    public string SessionId { get; }

    public AuditTrail(string? sessionId = null, SecretMasker? masker = null)
    {
        SessionId = sessionId ?? Guid.NewGuid().ToString("N");
        _masker = masker ?? SecretMasker.Default;
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    /// <summary>Records an agent action with automatic secret masking.</summary>
    public Result<AuditEvent> Record(
        string agentName,
        string eventType,
        string description,
        bool succeeded,
        AuditSeverity severity = AuditSeverity.Info,
        Error? securityError = null,
        IEnumerable<KeyValuePair<string, string>>? metadata = null)
    {
        try
        {
            var safeDescription = _masker.Mask(description);
            var safeMeta = metadata != null
                ? _masker.MaskDictionary(metadata)
                : (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();

            var ev = new AuditEvent
            {
                SessionId = SessionId,
                AgentName = agentName,
                EventType = eventType,
                Description = safeDescription,
                Severity = severity,
                Succeeded = succeeded,
                SecurityError = securityError,
                Metadata = safeMeta,
            };

            lock (_lock) { _events.Add(ev); }
            return Result<AuditEvent>.Success(ev);
        }
        catch (Exception ex)
        {
            return Result<AuditEvent>.Failure(SecurityError.AuditWriteFailed(ex.Message));
        }
    }

    /// <summary>Records a security violation (always severity=Security, succeeded=false).</summary>
    public Result<AuditEvent> RecordSecurityViolation(
        string agentName,
        string eventType,
        Error securityError,
        string? detail = null)
        => Record(agentName, eventType, detail ?? securityError.Message, false,
                  AuditSeverity.Security, securityError);

    // ── Read ──────────────────────────────────────────────────────────────────

    /// <summary>All recorded events, in chronological order.</summary>
    public IReadOnlyList<AuditEvent> Events
    {
        get { lock (_lock) return _events.ToList(); }
    }

    /// <summary>Only security-level events.</summary>
    public IEnumerable<AuditEvent> SecurityEvents
        => Events.Where(e => e.Severity >= AuditSeverity.Security);

    /// <summary>Only failed events.</summary>
    public IEnumerable<AuditEvent> Failures
        => Events.Where(e => !e.Succeeded);

    /// <summary>Events from a specific agent.</summary>
    public IEnumerable<AuditEvent> ForAgent(string agentName)
        => Events.Where(e => e.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase));

    /// <summary>True if any security violation has been recorded in this session.</summary>
    public bool HasSecurityViolations => Events.Any(e => e.Severity >= AuditSeverity.Security);

    /// <summary>Exports all events as a structured text report.</summary>
    public string ExportReport()
    {
        var events = Events;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== AuditTrail Report | Session {SessionId[..8]} | {events.Count} events ===");
        foreach (var e in events)
            sb.AppendLine(e.ToString());
        return sb.ToString();
    }
}
