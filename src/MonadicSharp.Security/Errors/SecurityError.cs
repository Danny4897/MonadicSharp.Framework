namespace MonadicSharp.Security.Errors;

/// <summary>
/// Typed security error factory. All security violations surface as
/// <see cref="Error"/> values in Result&lt;T&gt; — no silent failures, no unhandled exceptions.
/// </summary>
public static class SecurityError
{
    // ── Prompt injection ──────────────────────────────────────────────────────

    public static Error PromptInjectionDetected(string ruleName, string? excerpt = null)
        => Error.Create(
            $"Prompt injection detected by rule '{ruleName}'" + (excerpt != null ? $": ...{excerpt}..." : "."),
            "SECURITY_PROMPT_INJECTION");

    public static Error InputTooLong(int actual, int maxAllowed)
        => Error.Create(
            $"Input length {actual} exceeds maximum allowed {maxAllowed} characters.",
            "SECURITY_INPUT_TOO_LONG");

    public static Error InputContainsBinary()
        => Error.Create("Input contains non-printable/binary content.", "SECURITY_BINARY_INPUT");

    // ── Capability / authorization ────────────────────────────────────────────

    public static Error UnauthorizedCapabilityEscalation(string attemptedAction)
        => Error.Create(
            $"Unauthorized capability escalation attempt: '{attemptedAction}'.",
            "SECURITY_CAPABILITY_ESCALATION");

    public static Error PolicyViolation(string policyName, string detail)
        => Error.Create($"Security policy '{policyName}' violated: {detail}", "SECURITY_POLICY_VIOLATION");

    // ── Secret / data leakage ─────────────────────────────────────────────────

    public static Error SecretLeakageDetected(string fieldName)
        => Error.Create(
            $"Potential secret leakage detected in field '{fieldName}'.",
            "SECURITY_SECRET_LEAKAGE");

    public static Error OutputContainsMaskedSecret()
        => Error.Create("Agent output contains masked secret patterns.", "SECURITY_OUTPUT_LEAKED_SECRET");

    // ── Audit ─────────────────────────────────────────────────────────────────

    public static Error AuditWriteFailed(string reason)
        => Error.Create($"Audit trail write failed: {reason}", "SECURITY_AUDIT_FAILURE");

    // ── Content safety ────────────────────────────────────────────────────────

    public static Error ContentPolicyViolation(string category, float score)
        => Error.Create(
            $"Content policy violation in category '{category}' (score: {score:F2}).",
            "SECURITY_CONTENT_POLICY");

    public static Error UnknownThreat(string detail)
        => Error.Create($"Unknown security threat: {detail}", "SECURITY_UNKNOWN_THREAT");
}
