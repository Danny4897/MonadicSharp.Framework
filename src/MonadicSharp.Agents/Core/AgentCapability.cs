namespace MonadicSharp.Agents.Core;

/// <summary>
/// Declarative capability flags that govern what an agent is allowed to do.
/// Capabilities are granted per <see cref="AgentContext"/> and enforced at runtime
/// before any sensitive operation executes.
///
/// This is the foundation of MonadicSharp.Agents' capability-based security model:
/// agents must declare their <see cref="RequiredCapabilities"/> and the orchestrator
/// verifies they match the context's <see cref="AgentContext.GrantedCapabilities"/>
/// before execution begins.
/// </summary>
[Flags]
public enum AgentCapability : long
{
    /// <summary>No capabilities — the safest default.</summary>
    None = 0,

    // ── I/O ─────────────────────────────────────────────────────────────────

    /// <summary>Read files from the local filesystem.</summary>
    ReadLocalFiles = 1L << 0,

    /// <summary>Write or delete files on the local filesystem.</summary>
    WriteLocalFiles = 1L << 1,

    // ── Network ─────────────────────────────────────────────────────────────

    /// <summary>Make outbound HTTP/API calls.</summary>
    CallExternalApis = 1L << 2,

    /// <summary>Send messages (email, Slack, webhooks, etc.).</summary>
    SendMessages = 1L << 3,

    // ── Data ────────────────────────────────────────────────────────────────

    /// <summary>Query or write to a database.</summary>
    AccessDatabase = 1L << 4,

    /// <summary>Read secrets from environment variables or a secrets vault.</summary>
    ReadSecrets = 1L << 5,

    // ── Agent orchestration ──────────────────────────────────────────────────

    /// <summary>Spawn child agents (sub-agents). Agents without this cannot call other agents.</summary>
    SpawnSubAgents = 1L << 6,

    /// <summary>Access the execution traces and audit logs of other agents in the session.</summary>
    ReadAuditTrail = 1L << 7,

    // ── LLM ─────────────────────────────────────────────────────────────────

    /// <summary>Make calls to a language model API (counts toward quota).</summary>
    CallLlm = 1L << 8,

    /// <summary>Execute code (sandboxed or otherwise).</summary>
    ExecuteCode = 1L << 9,

    // ── Convenience groupings ────────────────────────────────────────────────

    /// <summary>Full read/write access to local files.</summary>
    LocalFileSystem = ReadLocalFiles | WriteLocalFiles,

    /// <summary>Outbound network (APIs + messaging).</summary>
    OutboundNetwork = CallExternalApis | SendMessages,

    /// <summary>All capabilities — use only in fully-trusted internal contexts.</summary>
    All = ~0L,
}

/// <summary>Extension helpers for <see cref="AgentCapability"/>.</summary>
public static class AgentCapabilityExtensions
{
    /// <summary>Returns true if <paramref name="granted"/> includes all flags in <paramref name="required"/>.</summary>
    public static bool Satisfies(this AgentCapability granted, AgentCapability required)
        => (granted & required) == required;

    /// <summary>Returns the capabilities in <paramref name="required"/> that are missing from <paramref name="granted"/>.</summary>
    public static AgentCapability Missing(this AgentCapability granted, AgentCapability required)
        => required & ~granted;

    /// <summary>Human-readable list of individual capability names.</summary>
    public static IEnumerable<string> ToNames(this AgentCapability capability)
    {
        foreach (AgentCapability flag in Enum.GetValues<AgentCapability>())
        {
            if (flag == AgentCapability.None || flag == AgentCapability.All) continue;
            // Skip composite values (more than one bit set)
            if ((flag & (flag - 1)) != 0) continue;
            if (capability.HasFlag(flag)) yield return flag.ToString();
        }
    }
}
