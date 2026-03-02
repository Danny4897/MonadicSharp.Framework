using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MonadicSharp.Caching.Core;

namespace MonadicSharp.Caching.Implementations;

/// <summary>
/// <see cref="ICacheService"/> backed by <see cref="IMemoryCache"/> (in-process).
/// Suitable for single-instance deployments; for distributed scenarios use
/// <see cref="DistributedCacheService"/>.
/// </summary>
public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService>? _logger;

    public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService>? logger = null)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<Result<T>> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(key, out var raw) && raw is T value)
        {
            _logger?.LogDebug("Cache HIT: {Key}", key);
            return Task.FromResult(Result<T>.Success(value));
        }

        _logger?.LogDebug("Cache MISS: {Key}", key);
        return Task.FromResult(Result<T>.Failure(CacheError.Miss(key)));
    }

    public Task<Result<Unit>> SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var opts = options ?? CacheEntryOptions.Default;
            using var entry = _cache.CreateEntry(key);
            entry.Value = value;

            if (opts.AbsoluteExpiration.HasValue)
                entry.AbsoluteExpirationRelativeToNow = opts.AbsoluteExpiration;
            if (opts.SlidingExpiration.HasValue)
                entry.SlidingExpiration = opts.SlidingExpiration;

            _logger?.LogDebug("Cache SET: {Key}", key);
            return Task.FromResult(Result<Unit>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Cache SET failed for key {Key}", key);
            return Task.FromResult(Result<Unit>.Failure(CacheError.StoreError("SET", key, ex)));
        }
    }

    public Task<Result<Unit>> RemoveAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _cache.Remove(key);
        _logger?.LogDebug("Cache REMOVE: {Key}", key);
        return Task.FromResult(Result<Unit>.Success(Unit.Value));
    }

    public async Task<Result<T>> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<Result<T>>> factory,
        CacheEntryOptions? options = null,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct).ConfigureAwait(false);
        if (cached.IsSuccess)
            return cached;

        var produced = await factory(ct).ConfigureAwait(false);
        if (produced.IsFailure)
            return produced;

        // Best-effort store — never mask the factory result
        var stored = await SetAsync(key, produced.Value, options, ct).ConfigureAwait(false);
        if (stored.IsFailure)
            _logger?.LogWarning("Cache SET failed after factory for key {Key}: {Error}", key, stored.Error.Message);

        return produced;
    }
}
