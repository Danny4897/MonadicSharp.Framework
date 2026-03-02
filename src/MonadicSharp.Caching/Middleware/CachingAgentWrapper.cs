using Microsoft.Extensions.Logging;
using MonadicSharp.Agents;
using MonadicSharp.Agents.Core;
using MonadicSharp.Caching.Core;

namespace MonadicSharp.Caching.Middleware;

/// <summary>
/// Transparent decorator that caches agent output for identical inputs.
///
/// On cache HIT  → returns the cached result without calling the inner agent.
/// On cache MISS → calls the inner agent; stores the result if the policy allows it.
///
/// The decorator is transparent: it exposes the same <see cref="Name"/> and
/// <see cref="RequiredCapabilities"/> as the inner agent.
///
/// Usage:
/// <code>
/// IAgent&lt;Query, SearchResult&gt; cachedSearch =
///     new CachingAgentWrapper&lt;Query, SearchResult&gt;(
///         searchAgent,
///         cache,
///         new AgentCachePolicy&lt;Query, SearchResult&gt;
///         {
///             KeyFactory = (name, q) => $"{name}:{q.Text}:{q.TopK}",
///             EntryOptions = CacheEntryOptions.WithTtl(TimeSpan.FromMinutes(10))
///         });
/// </code>
/// </summary>
public sealed class CachingAgentWrapper<TInput, TOutput> : IAgent<TInput, TOutput>
{
    private readonly IAgent<TInput, TOutput> _inner;
    private readonly ICacheService _cache;
    private readonly AgentCachePolicy<TInput, TOutput> _policy;
    private readonly ILogger<CachingAgentWrapper<TInput, TOutput>>? _logger;

    public string Name => _inner.Name;
    public AgentCapability RequiredCapabilities => _inner.RequiredCapabilities;

    public CachingAgentWrapper(
        IAgent<TInput, TOutput> inner,
        ICacheService cache,
        AgentCachePolicy<TInput, TOutput>? policy = null,
        ILogger<CachingAgentWrapper<TInput, TOutput>>? logger = null)
    {
        _inner = inner;
        _cache = cache;
        _policy = policy ?? new AgentCachePolicy<TInput, TOutput>();
        _logger = logger;
    }

    public async Task<Result<TOutput>> ExecuteAsync(
        TInput input,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        // Bypass if the policy says so
        if (_policy.BypassPredicate?.Invoke(input, context) == true)
        {
            _logger?.LogDebug("Cache bypass for agent {Agent}", Name);
            return await _inner.ExecuteAsync(input, context, cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = _policy.KeyFactory(Name, input);

        // Try cache first
        var cached = await _cache.GetAsync<TOutput>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached.IsSuccess)
        {
            _logger?.LogDebug("Agent {Agent} cache HIT for key {Key}", Name, cacheKey);
            return cached;
        }

        // Cache miss — execute the agent
        _logger?.LogDebug("Agent {Agent} cache MISS for key {Key}", Name, cacheKey);
        var result = await _inner.ExecuteAsync(input, context, cancellationToken).ConfigureAwait(false);

        // Store in cache if policy permits
        if (result.IsSuccess || !_policy.CacheOnlySuccesses)
        {
            if (result.IsSuccess)
            {
                var stored = await _cache.SetAsync(cacheKey, result.Value, _policy.EntryOptions, cancellationToken)
                    .ConfigureAwait(false);
                if (stored.IsFailure)
                    _logger?.LogWarning("Agent {Agent}: cache SET failed for key {Key}: {Error}", Name, cacheKey, stored.Error.Message);
            }
        }

        return result;
    }
}
