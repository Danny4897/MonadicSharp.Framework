using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MonadicSharp.Caching.Core;

namespace MonadicSharp.Caching.Implementations;

/// <summary>
/// <see cref="ICacheService"/> backed by <see cref="IDistributedCache"/> (Redis, SQL, etc.).
/// Serializes values with <see cref="System.Text.Json"/>; the serializer options can be
/// customised via the constructor.
/// </summary>
public sealed class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<DistributedCacheService>? _logger;

    public DistributedCacheService(
        IDistributedCache cache,
        JsonSerializerOptions? jsonOptions = null,
        ILogger<DistributedCacheService>? logger = null)
    {
        _cache = cache;
        _jsonOptions = jsonOptions ?? JsonSerializerOptions.Default;
        _logger = logger;
    }

    public async Task<Result<T>> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var bytes = await _cache.GetAsync(key, ct).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                _logger?.LogDebug("Cache MISS: {Key}", key);
                return Result<T>.Failure(CacheError.Miss(key));
            }

            _logger?.LogDebug("Cache HIT: {Key}", key);
            var value = JsonSerializer.Deserialize<T>(bytes, _jsonOptions);
            return value is null
                ? Result<T>.Failure(CacheError.Miss(key))
                : Result<T>.Success(value);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Cache deserialization failed for key {Key}", key);
            return Result<T>.Failure(CacheError.DeserializationFailed(key, typeof(T), ex));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Cache GET failed for key {Key}", key);
            return Result<T>.Failure(CacheError.StoreError("GET", key, ex));
        }
    }

    public async Task<Result<Unit>> SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default)
    {
        try
        {
            byte[] bytes;
            try
            {
                bytes = JsonSerializer.SerializeToUtf8Bytes(value, _jsonOptions);
            }
            catch (Exception ex)
            {
                return Result<Unit>.Failure(CacheError.SerializationFailed(key, ex));
            }

            var opts = options ?? CacheEntryOptions.Default;
            var distOpts = new DistributedCacheEntryOptions();

            if (opts.AbsoluteExpiration.HasValue)
                distOpts.AbsoluteExpirationRelativeToNow = opts.AbsoluteExpiration;
            if (opts.SlidingExpiration.HasValue)
                distOpts.SlidingExpiration = opts.SlidingExpiration;

            await _cache.SetAsync(key, bytes, distOpts, ct).ConfigureAwait(false);
            _logger?.LogDebug("Cache SET: {Key} ({Bytes} bytes)", key, bytes.Length);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Cache SET failed for key {Key}", key);
            return Result<Unit>.Failure(CacheError.StoreError("SET", key, ex));
        }
    }

    public async Task<Result<Unit>> RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(key, ct).ConfigureAwait(false);
            _logger?.LogDebug("Cache REMOVE: {Key}", key);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Cache REMOVE failed for key {Key}", key);
            return Result<Unit>.Failure(CacheError.StoreError("REMOVE", key, ex));
        }
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

        var stored = await SetAsync(key, produced.Value, options, ct).ConfigureAwait(false);
        if (stored.IsFailure)
            _logger?.LogWarning("Cache SET failed after factory for key {Key}: {Error}", key, stored.Error.Message);

        return produced;
    }
}
