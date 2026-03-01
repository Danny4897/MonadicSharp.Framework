using MonadicSharp.Agents.Core;

namespace MonadicSharp.Agents;

/// <summary>
/// The fundamental typed contract for all agents in the MonadicSharp.Agents framework.
///
/// An agent is a stateless, composable unit of work that:
/// 1. Declares the capabilities it needs (<see cref="RequiredCapabilities"/>)
/// 2. Accepts a strongly-typed input and returns a strongly-typed output
/// 3. Always returns a <see cref="Result{T}"/> — never throws for expected failures
/// 4. Receives an <see cref="AgentContext"/> for capability verification and correlation
///
/// The orchestrator checks <see cref="RequiredCapabilities"/> against the context's
/// granted capabilities before calling <see cref="ExecuteAsync"/>, so agents can
/// assume at runtime that their required capabilities are available.
/// </summary>
/// <typeparam name="TInput">The type of data this agent accepts.</typeparam>
/// <typeparam name="TOutput">The type of data this agent produces on success.</typeparam>
public interface IAgent<TInput, TOutput>
{
    /// <summary>Human-readable name used in traces and audit logs.</summary>
    string Name { get; }

    /// <summary>
    /// Capabilities this agent needs to function.
    /// The orchestrator enforces these before execution.
    /// </summary>
    AgentCapability RequiredCapabilities { get; }

    /// <summary>
    /// Executes the agent's logic.
    /// Implementations must NOT throw for expected failures — return
    /// <see cref="Result{T}.Failure"/> instead so the pipeline can handle them gracefully.
    /// </summary>
    Task<Result<TOutput>> ExecuteAsync(TInput input, AgentContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Synchronous variant — prefer the async version for I/O-bound work.
/// </summary>
public interface ISyncAgent<TInput, TOutput>
{
    string Name { get; }
    AgentCapability RequiredCapabilities { get; }
    Result<TOutput> Execute(TInput input, AgentContext context);
}

/// <summary>Extension methods for working with <see cref="IAgent{TInput,TOutput}"/>.</summary>
public static class AgentExtensions
{
    /// <summary>Wraps a synchronous agent to satisfy the async interface.</summary>
    public static Task<Result<TOutput>> ExecuteAsync<TInput, TOutput>(
        this ISyncAgent<TInput, TOutput> agent,
        TInput input,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(agent.Execute(input, context));
    }
}
