using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MonadicSharp.Persistence.Core;

namespace MonadicSharp.Persistence.Extensions;

/// <summary>
/// LINQ/EF Core extension methods that return <see cref="Result{T}"/> values
/// instead of throwing or returning null, integrating queries into Railway pipelines.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Materializes the query to a list wrapped in a <see cref="Result{T}"/>.
    /// Database errors are caught and wrapped in <see cref="PersistenceError.DatabaseError"/>.
    /// </summary>
    public static async Task<Result<IReadOnlyList<T>>> ToResultAsync<T>(
        this IQueryable<T> query,
        CancellationToken ct = default)
    {
        try
        {
            var list = await query.ToListAsync(ct);
            return Result<IReadOnlyList<T>>.Success(list);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<T>>.Failure(
                PersistenceError.DatabaseError(nameof(ToResultAsync), ex));
        }
    }

    /// <summary>
    /// Returns the first element matching <paramref name="predicate"/>,
    /// or <see cref="PersistenceError.NotFound"/> if none match.
    /// </summary>
    public static async Task<Result<T>> FirstOrDefaultResultAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        try
        {
            var entity = await query.FirstOrDefaultAsync(predicate, ct);
            return entity is not null
                ? Result<T>.Success(entity)
                : Result<T>.Failure(PersistenceError.NotFound(typeof(T).Name, "predicate"));
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(
                PersistenceError.DatabaseError(nameof(FirstOrDefaultResultAsync), ex));
        }
    }

    /// <summary>
    /// Returns the first element, or <see cref="PersistenceError.NotFound"/> if the sequence is empty.
    /// </summary>
    public static async Task<Result<T>> FirstOrDefaultResultAsync<T>(
        this IQueryable<T> query,
        CancellationToken ct = default)
    {
        try
        {
            var entity = await query.FirstOrDefaultAsync(ct);
            return entity is not null
                ? Result<T>.Success(entity)
                : Result<T>.Failure(PersistenceError.NotFound(typeof(T).Name, "first"));
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(
                PersistenceError.DatabaseError(nameof(FirstOrDefaultResultAsync), ex));
        }
    }

    /// <summary>
    /// Returns exactly one element, or an error:
    /// <see cref="PersistenceError.NotFound"/> if none, <see cref="PersistenceError.TooManyResults"/> if more than one.
    /// </summary>
    public static async Task<Result<T>> SingleResultAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        try
        {
            var results = await query.Where(predicate).Take(2).ToListAsync(ct);
            return results.Count switch
            {
                0 => Result<T>.Failure(PersistenceError.NotFound(typeof(T).Name, "predicate")),
                1 => Result<T>.Success(results[0]),
                _ => Result<T>.Failure(PersistenceError.TooManyResults(typeof(T).Name, results.Count))
            };
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(
                PersistenceError.DatabaseError(nameof(SingleResultAsync), ex));
        }
    }

    /// <summary>
    /// Returns the count of elements as a <see cref="Result{T}"/>.
    /// </summary>
    public static async Task<Result<int>> CountResultAsync<T>(
        this IQueryable<T> query,
        CancellationToken ct = default)
    {
        try
        {
            var count = await query.CountAsync(ct);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure(
                PersistenceError.DatabaseError(nameof(CountResultAsync), ex));
        }
    }
}
