using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MonadicSharp.Caching.Core;
using MonadicSharp.Caching.Implementations;

namespace MonadicSharp.Caching.Extensions;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for MonadicSharp.Caching.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MemoryCacheService"/> as the <see cref="ICacheService"/> singleton.
    /// Requires <c>AddMemoryCache()</c> to be called first (or use the overload that calls it).
    /// </summary>
    public static IServiceCollection AddMonadicSharpMemoryCache(
        this IServiceCollection services,
        bool addMemoryCache = true)
    {
        if (addMemoryCache)
            services.AddMemoryCache();

        services.TryAddSingleton<ICacheService, MemoryCacheService>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="DistributedCacheService"/> as the <see cref="ICacheService"/> singleton.
    /// Requires an <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> registration
    /// (e.g. <c>AddStackExchangeRedisCache()</c> or <c>AddDistributedMemoryCache()</c>).
    /// </summary>
    public static IServiceCollection AddMonadicSharpDistributedCache(
        this IServiceCollection services)
    {
        services.TryAddSingleton<ICacheService, DistributedCacheService>();
        return services;
    }
}
