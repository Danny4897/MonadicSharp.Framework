namespace MonadicSharp.Caching.Core;

/// <summary>
/// A Result-aware cache service. Every operation returns a typed <see cref="Result{T}"/>:
/// cache misses surface as <see cref="CacheError.Miss"/>, serialization problems as
/// <see cref="CacheError.DeserializationFailed"/> — never null, never an unhandled exception.
///
/// This makes caching a first-class citizen in Railway-Oriented pipelines:
/// <code>
/// var result = await cache.GetAsync&lt;UserProfile&gt;(userId)
///     .BindAsync(profile => profile is not null
///         ? Result&lt;UserProfile&gt;.Success(profile)
///         : repository.FindAsync(userId));
/// </code>
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value, or <see cref="CacheError.Miss"/> if the key is not present.
    /// </summary>
    Task<Result<T>> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Stores a value in the cache with the provided options.
    /// Returns <see cref="Result{Unit}"/> — failures are typed, not exceptions.
    /// </summary>
    Task<Result<Unit>> SetAsync<T>(string key, T value, CacheEntryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Removes a key from the cache.
    /// Always succeeds (idempotent): removing a missing key returns <see cref="Result{Unit}.Success"/>.
    /// </summary>
    Task<Result<Unit>> RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns the cached value if present; otherwise executes <paramref name="factory"/>,
    /// stores the result, and returns it. Cache errors from Set are swallowed (logged) —
    /// the factory result is always returned.
    /// </summary>
    Task<Result<T>> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<Result<T>>> factory,
        CacheEntryOptions? options = null,
        CancellationToken ct = default);
}
