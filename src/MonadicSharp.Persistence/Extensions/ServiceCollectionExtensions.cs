using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MonadicSharp.Persistence.Core;
using MonadicSharp.Persistence.Implementations;

namespace MonadicSharp.Persistence.Extensions;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for MonadicSharp.Persistence.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF Core <see cref="IUnitOfWork"/> implementation using the specified
    /// <typeparamref name="TContext"/> as the underlying <see cref="DbContext"/>.
    /// </summary>
    public static IServiceCollection AddMonadicSharpPersistence<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="EfCoreRepository{T,TId}"/> as <see cref="IRepository{T,TId}"/>
    /// and <see cref="IReadRepository{T,TId}"/> for the given entity type.
    /// </summary>
    public static IServiceCollection AddMonadicSharpRepository<T, TId>(
        this IServiceCollection services)
        where T : class
    {
        services.AddScoped<IRepository<T, TId>, EfCoreRepository<T, TId>>();
        services.AddScoped<IReadRepository<T, TId>>(sp => sp.GetRequiredService<IRepository<T, TId>>());
        return services;
    }

    /// <summary>
    /// Convenience method that registers both the <see cref="IUnitOfWork"/> and a repository
    /// for a given entity type in a single call.
    /// </summary>
    public static IServiceCollection AddMonadicSharpPersistence<TContext, T, TId>(
        this IServiceCollection services)
        where TContext : DbContext
        where T : class
    {
        return services
            .AddMonadicSharpPersistence<TContext>()
            .AddMonadicSharpRepository<T, TId>();
    }
}
