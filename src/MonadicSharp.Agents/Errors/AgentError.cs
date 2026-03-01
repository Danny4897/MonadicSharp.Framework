using MonadicSharp.Agents.Core;

namespace MonadicSharp.Agents.Errors;

/// <summary>
/// Typed error factory for all failures that can occur within the MonadicSharp.Agents framework.
/// All errors are plain <see cref="Error"/> values — they flow through Result&lt;T&gt; without
/// ever causing unhandled exceptions.
/// </summary>
public static class AgentError
{
    // ── Capability errors ─────────────────────────────────────────────────────

    public static Error InsufficientCapabilities(AgentCapability required, AgentCapability missing)
        => Error.Create(
            $"Agent requires [{string.Join(", ", required.ToNames())}] but [{string.Join(", ", missing.ToNames())}] were not granted.",
            "AGENT_CAPABILITY_DENIED");

    public static Error CapabilityCheckFailed(string agentName, AgentCapability required, AgentCapability granted)
        => Error.Create(
            $"Agent '{agentName}' requires [{string.Join(", ", required.ToNames())}]; context only grants [{string.Join(", ", granted.ToNames())}].",
            "AGENT_AUTHORIZATION_FAILED");

    // ── Pipeline errors ───────────────────────────────────────────────────────

    public static Error PipelineStepFailed(string stepName, string agentName, Error inner)
        => Error.Create(
            $"Pipeline step '{stepName}' (agent: {agentName}) failed: {inner.Message}",
            "AGENT_PIPELINE_STEP_FAILED")
           .WithInnerError(inner);

    public static Error PipelineAborted(string reason)
        => Error.Create($"Pipeline aborted: {reason}", "AGENT_PIPELINE_ABORTED");

    public static Error ParallelExecutionFailed(IEnumerable<(string name, Error error)> failures)
    {
        var details = string.Join("; ", failures.Select(f => $"[{f.name}]: {f.error.Message}"));
        return Error.Create($"Parallel agent execution had failures — {details}", "AGENT_PARALLEL_FAILURES");
    }

    // ── Circuit breaker ───────────────────────────────────────────────────────

    public static Error CircuitOpen(string circuitName, int failureCount, TimeSpan openDuration)
        => Error.Create(
            $"Circuit '{circuitName}' is OPEN after {failureCount} failures. Retry after {openDuration.TotalSeconds:F1}s.",
            "AGENT_CIRCUIT_OPEN");

    public static Error CircuitHalfOpenRejected(string circuitName)
        => Error.Create(
            $"Circuit '{circuitName}' is in HALF-OPEN state and already has a probe in flight.",
            "AGENT_CIRCUIT_HALF_OPEN_BUSY");

    // ── Timeout / cancellation ────────────────────────────────────────────────

    public static Error Timeout(string agentName, TimeSpan timeout)
        => Error.Create(
            $"Agent '{agentName}' exceeded timeout of {timeout.TotalMilliseconds:F0}ms.",
            "AGENT_TIMEOUT");

    public static Error Cancelled(string agentName)
        => Error.Create($"Agent '{agentName}' was cancelled.", "AGENT_CANCELLED");

    // ── Orchestration ─────────────────────────────────────────────────────────

    public static Error AgentNotFound(string agentName)
        => Error.Create($"No agent registered with name '{agentName}'.", "AGENT_NOT_FOUND");

    public static Error TypeMismatch(string agentName, Type expected, Type actual)
        => Error.Create(
            $"Agent '{agentName}' expects input type '{expected.Name}' but received '{actual.Name}'.",
            "AGENT_TYPE_MISMATCH");

    // ── Metadata ──────────────────────────────────────────────────────────────

    public static Error MetadataKeyNotFound(string key)
        => Error.Create($"Metadata key '{key}' not found in AgentContext.", "AGENT_METADATA_NOT_FOUND");

    public static Error MetadataTypeMismatch(string key, Type expected, Type actual)
        => Error.Create(
            $"Metadata key '{key}' is of type '{actual.Name}', expected '{expected.Name}'.",
            "AGENT_METADATA_TYPE_MISMATCH");

    // ── Unhandled exception wrapper ───────────────────────────────────────────

    public static Error UnhandledException(string agentName, Exception ex)
        => Error.FromException(ex, "AGENT_UNHANDLED_EXCEPTION")
                .WithMetadata("AgentName", agentName);
}
