# MonadicSharp.Caching

> Result-aware caching for AI agent pipelines — cache misses and errors are typed `Result<T>` values, never null or exceptions.

[![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Caching)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)

---

## Overview

`MonadicSharp.Caching` integrates caching into Railway-Oriented pipelines:

- **`ICacheService`** — unified Result-aware interface for memory and distributed caches
- **`MemoryCacheService`** — backed by `IMemoryCache`, serializes via JSON
- **`DistributedCacheService`** — backed by `IDistributedCache` (Redis, SQL Server, etc.)
- **`CachingAgentWrapper`** — transparent decorator: caches agent output for identical inputs
- **`AgentCachePolicy`** — configurable key factory, TTL, bypass predicate

---

## Installation

```bash
dotnet add package MonadicSharp.Caching
```

---

## ICacheService

```csharp
// Get — returns CacheError.Miss on miss, never null
Result<UserProfile> cached = await cache.GetAsync<UserProfile>("user:42");

// Set
await cache.SetAsync("user:42", profile, new CacheEntryOptions
{
    AbsoluteExpiration = TimeSpan.FromMinutes(10)
});

// Remove (idempotent — removing a missing key is always Success)
await cache.RemoveAsync("user:42");

// GetOrSet — the Railway-friendly pattern
var result = await cache.GetOrSetAsync(
    key: $"user:{userId}",
    factory: ct => repository.FindAsync(userId, ct),
    options: CacheEntryOptions.WithTtl(TimeSpan.FromMinutes(5)));
```

---

## CacheEntryOptions

```csharp
// Absolute TTL
CacheEntryOptions.WithTtl(TimeSpan.FromMinutes(10))

// Sliding TTL (reset on each access)
new CacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) }

// Size (for IMemoryCache with size limits)
new CacheEntryOptions { Size = 1 }
```

---

## CachingAgentWrapper

Wraps any `IAgent<TInput, TOutput>` with transparent output caching.

```csharp
IAgent<Query, SearchResult> cachedSearch = new CachingAgentWrapper<Query, SearchResult>(
    inner: searchAgent,
    cache: cache,
    policy: new AgentCachePolicy<Query, SearchResult>
    {
        // Key derivation from agent name + input
        KeyFactory = (name, q) => $"{name}:{q.Text}:{q.TopK}",

        // Cache TTL
        EntryOptions = CacheEntryOptions.WithTtl(TimeSpan.FromMinutes(10)),

        // Only cache successful results (default: true)
        CacheOnlySuccesses = true,

        // Skip cache for certain inputs
        BypassPredicate = (q, ctx) => q.SkipCache
    });
```

The wrapper exposes the same `Name` and `RequiredCapabilities` as the inner agent — it is completely transparent to the pipeline.

---

## DI Registration

```csharp
// IMemoryCache-backed (in-process)
services.AddMonadicSharpMemoryCache();

// IDistributedCache-backed (Redis, SQL Server, etc.)
services.AddStackExchangeRedisCache(opts => opts.Configuration = "localhost");
services.AddMonadicSharpDistributedCache();
```

---

## Error Codes

| Code | Meaning |
|------|---------|
| `CACHE_MISS` | Key not found in cache |
| `CACHE_DESERIALIZATION_FAILED` | Cached bytes could not be deserialized |
| `CACHE_SERIALIZATION_FAILED` | Value could not be serialized before storing |
| `CACHE_STORE_ERROR` | Underlying cache store threw an exception |

---

## License

MIT — part of [MonadicSharp.Framework](../../README.md).
