using MonadicSharp.Agents.Core;
using MonadicSharp.Security.Audit;
using MonadicSharp.Security.Guard;
using MonadicSharp.Security.Masking;

namespace MonadicSharp.Security.Core;

/// <summary>
/// An <see cref="AgentContext"/> enhanced with security services:
/// a <see cref="PromptGuard"/>, an <see cref="AuditTrail"/>, and a <see cref="SecretMasker"/>.
///
/// This is the entry point for the MonadicSharp.Security layer.
/// Instead of creating bare AgentContexts, use <see cref="SecureAgentContext.Create"/>
/// to get a context that automatically validates inputs and records all operations.
/// </summary>
public sealed class SecureAgentContext
{
    public AgentContext Context { get; }
    public AuditTrail Audit { get; }
    public PromptGuard Guard { get; }
    public SecretMasker Masker { get; }

    private SecureAgentContext(AgentContext context, AuditTrail audit, PromptGuard guard, SecretMasker masker)
    {
        Context = context;
        Audit = audit;
        Guard = guard;
        Masker = masker;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static SecureAgentContext Create(
        AgentCapability capabilities,
        PromptGuardOptions? guardOptions = null,
        SecretMasker? masker = null,
        CancellationToken cancellationToken = default)
    {
        var context = AgentContext.Create(capabilities, cancellationToken);
        var audit = new AuditTrail(context.SessionId, masker);
        var guard = new PromptGuard(guardOptions ?? PromptGuardOptions.Default);
        return new SecureAgentContext(context, audit, guard, masker ?? SecretMasker.Default);
    }

    public static SecureAgentContext Trusted(CancellationToken cancellationToken = default)
        => Create(AgentCapability.All, cancellationToken: cancellationToken);

    public static SecureAgentContext Sandboxed(CancellationToken cancellationToken = default)
        => Create(AgentCapability.None, PromptGuardOptions.Strict, cancellationToken: cancellationToken);

    // ── Secure input validation ────────────────────────────────────────────────

    /// <summary>
    /// Validates user input through the PromptGuard and records the check in the audit trail.
    /// Returns the safe input on success, or a typed SecurityError on injection detection.
    /// </summary>
    public Result<string> ValidateInput(string input, string agentName = "InputValidation")
    {
        var guardResult = Guard.Validate(input);

        if (guardResult.IsFailure)
        {
            Audit.RecordSecurityViolation(agentName, "PromptInjectionAttempt", guardResult.Error);
            return guardResult;
        }

        Audit.Record(agentName, "InputValidated", $"Input validated ({input.Length} chars)", true);
        return guardResult;
    }

    /// <summary>Narrows capabilities for a child agent while preserving audit/guard context.</summary>
    public SecureAgentContext Narrow(AgentCapability allowedSubset)
        => new(Context.Narrow(allowedSubset), Audit, Guard, Masker);

    public override string ToString()
        => $"SecureAgentContext[{Context.SessionId[..8]}] Violations={Audit.HasSecurityViolations}";
}
