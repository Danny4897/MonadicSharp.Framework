namespace MonadicSharp.Caching.Core;

/// <summary>
/// Typed error factory for all cache failures.
/// All errors are <see cref="Error"/> values — they flow through Result pipelines
/// without causing unhandled exceptions.
/// </summary>
public static class CacheError
{
    /// <summary>The requested key was not found in the cache.</summary>
    public static Error Miss(string key) =>
        Error.Create($"Cache miss for key '{key}'.", "CACHE_MISS");

    /// <summary>The cached bytes could not be deserialized to the requested type.</summary>
    public static Error DeserializationFailed(string key, Type targetType, Exception ex) =>
        Error.FromException(ex, "CACHE_DESERIALIZATION_FAILED")
             .WithMetadata("CacheKey", key)
             .WithMetadata("TargetType", targetType.Name);

    /// <summary>Serialization of the value to bytes failed.</summary>
    public static Error SerializationFailed(string key, Exception ex) =>
        Error.FromException(ex, "CACHE_SERIALIZATION_FAILED")
             .WithMetadata("CacheKey", key);

    /// <summary>The underlying cache store threw an unexpected exception.</summary>
    public static Error StoreError(string operation, string key, Exception ex) =>
        Error.FromException(ex, "CACHE_STORE_ERROR")
             .WithMetadata("CacheOperation", operation)
             .WithMetadata("CacheKey", key);
}
