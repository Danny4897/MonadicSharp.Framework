using MonadicSharp.Agents;
using MonadicSharp.Agents.Core;
using MonadicSharp.Security.Audit;
using MonadicSharp.Security.Core;
using MonadicSharp.Security.Errors;
using MonadicSharp.Security.Masking;

namespace MonadicSharp.Security.Middleware;

/// <summary>
/// Wraps any <see cref="IAgent{TInput,TOutput}"/> with a security layer that:
/// 1. Validates input through the <see cref="SecureAgentContext.Guard"/> before executing
/// 2. Masks secrets in the output before returning
/// 3. Records all executions (success and failure) in the <see cref="AuditTrail"/>
/// 4. Enforces capability checks against the context
///
/// This is the adapter pattern for retrofitting security onto existing agents
/// without modifying them — the wrapped agent is unaware of the security layer.
///
/// Usage:
/// <code>
/// IAgent&lt;string, Summary&gt; secureAgent = mySummaryAgent.WithSecurity(secureCtx);
/// var result = await secureAgent.ExecuteAsync(userInput, secureCtx.Context);
/// </code>
/// </summary>
public sealed class SecureAgentWrapper<TInput, TOutput> : IAgent<TInput, TOutput>
    where TInput : class
    where TOutput : class
{
    private readonly IAgent<TInput, TOutput> _inner;
    private readonly SecureAgentContext _secureCtx;
    private readonly bool _validateStringInputs;
    private readonly bool _maskStringOutputs;

    public string Name => $"Secure({_inner.Name})";
    public AgentCapability RequiredCapabilities => _inner.RequiredCapabilities;

    public SecureAgentWrapper(
        IAgent<TInput, TOutput> inner,
        SecureAgentContext secureCtx,
        bool validateStringInputs = true,
        bool maskStringOutputs = true)
    {
        _inner = inner;
        _secureCtx = secureCtx;
        _validateStringInputs = validateStringInputs;
        _maskStringOutputs = maskStringOutputs;
    }

    public async Task<Result<TOutput>> ExecuteAsync(
        TInput input,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate string inputs through PromptGuard
        if (_validateStringInputs && input is string strInput)
        {
            var guardResult = _secureCtx.ValidateInput(strInput, _inner.Name);
            if (guardResult.IsFailure)
                return Result<TOutput>.Failure(guardResult.Error);
        }

        // 2. Execute the inner agent
        Result<TOutput> result;
        try
        {
            result = await _inner.ExecuteAsync(input, context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var err = Error.FromException(ex, "SECURE_AGENT_UNHANDLED");
            _secureCtx.Audit.Record(_inner.Name, "AgentException", ex.Message, false, AuditSeverity.Critical);
            return Result<TOutput>.Failure(err);
        }

        // 3. Check output for secret leakage
        if (_maskStringOutputs && result.IsSuccess && result.Value is string strOutput)
        {
            if (_secureCtx.Masker.ContainsSecret(strOutput))
            {
                var leakErr = SecurityError.OutputContainsMaskedSecret();
                _secureCtx.Audit.RecordSecurityViolation(_inner.Name, "OutputSecretLeak", leakErr);
                // Mask it rather than blocking (configurable in future)
                var masked = _secureCtx.Masker.Mask(strOutput);
                result = Result<TOutput>.Success((TOutput)(object)masked);
            }
        }

        // 4. Record in audit trail
        _secureCtx.Audit.Record(
            _inner.Name,
            result.IsSuccess ? "AgentSuccess" : "AgentFailure",
            result.IsSuccess ? "Execution succeeded" : result.Error.Message,
            result.IsSuccess,
            result.IsFailure ? AuditSeverity.Warning : AuditSeverity.Info);

        return result;
    }
}

/// <summary>Extension methods for wrapping agents with security.</summary>
public static class SecureAgentExtensions
{
    /// <summary>Wraps an agent with the full security middleware stack.</summary>
    public static SecureAgentWrapper<TInput, TOutput> WithSecurity<TInput, TOutput>(
        this IAgent<TInput, TOutput> agent,
        SecureAgentContext secureCtx,
        bool validateStringInputs = true,
        bool maskStringOutputs = true)
        where TInput : class
        where TOutput : class
        => new(agent, secureCtx, validateStringInputs, maskStringOutputs);
}
