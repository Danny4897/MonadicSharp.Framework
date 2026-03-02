namespace MonadicSharp.Caching.Core;

/// <summary>
/// Immutable configuration for a single cache entry.
/// Defaults to a 5-minute absolute expiration with no sliding window.
/// </summary>
public sealed record CacheEntryOptions
{
    /// <summary>
    /// How long the entry lives from the moment it is inserted.
    /// Null means the entry never expires (use with caution).
    /// </summary>
    public TimeSpan? AbsoluteExpiration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Reset the TTL on every access. Null disables sliding expiration.
    /// If both are set, the entry expires at whichever comes first.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; init; }

    // ── Presets ───────────────────────────────────────────────────────────────

    public static readonly CacheEntryOptions Default = new();

    public static readonly CacheEntryOptions Short =
        new() { AbsoluteExpiration = TimeSpan.FromMinutes(1) };

    public static readonly CacheEntryOptions Long =
        new() { AbsoluteExpiration = TimeSpan.FromHours(1) };

    public static readonly CacheEntryOptions NoExpiry =
        new() { AbsoluteExpiration = null };

    public static CacheEntryOptions WithTtl(TimeSpan ttl) =>
        new() { AbsoluteExpiration = ttl };

    public static CacheEntryOptions WithSliding(TimeSpan window) =>
        new() { SlidingExpiration = window, AbsoluteExpiration = null };
}
