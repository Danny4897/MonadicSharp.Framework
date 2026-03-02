using MonadicSharp.Agents.Core;
using MonadicSharp.Caching.Core;

namespace MonadicSharp.Caching.Middleware;

/// <summary>
/// Controls how <see cref="CachingAgentWrapper{TInput,TOutput}"/> caches results.
/// </summary>
public sealed class AgentCachePolicy<TInput, TOutput>
{
    /// <summary>
    /// Derives a cache key from the agent name and input.
    /// Default: <c>"{agentName}:{input}"</c> via <see cref="object.ToString"/>.
    /// Override for composite inputs that require stable key derivation.
    /// </summary>
    public Func<string, TInput, string> KeyFactory { get; init; } =
        (agentName, input) => $"{agentName}:{input}";

    /// <summary>TTL and sliding expiration for successful results.</summary>
    public CacheEntryOptions EntryOptions { get; init; } = CacheEntryOptions.Default;

    /// <summary>
    /// When <c>true</c>, failed results are NOT cached — only successful ones are stored.
    /// Default: <c>true</c>. Set to <c>false</c> only for idempotent negative results.
    /// </summary>
    public bool CacheOnlySuccesses { get; init; } = true;

    /// <summary>
    /// Optional predicate to bypass the cache for specific inputs.
    /// Returning <c>true</c> means "skip cache, always call the agent".
    /// </summary>
    public Func<TInput, AgentContext, bool>? BypassPredicate { get; init; }
}
