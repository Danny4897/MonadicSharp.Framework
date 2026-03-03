using System.Linq.Expressions;

namespace MonadicSharp.Persistence.Core;

/// <summary>
/// Read-only repository for a given entity type.
/// Every operation returns a typed <see cref="Result{T}"/>:
/// missing entities surface as <see cref="PersistenceError.NotFound"/>,
/// database errors as <see cref="PersistenceError.DatabaseError"/> —
/// never null, never an unhandled exception.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TId">The type of the entity's primary key.</typeparam>
public interface IReadRepository<T, TId>
    where T : class
{
    /// <summary>
    /// Returns the entity by primary key, or <see cref="PersistenceError.NotFound"/> if absent.
    /// </summary>
    Task<Result<T>> FindAsync(TId id, CancellationToken ct = default);

    /// <summary>
    /// Returns the first entity matching <paramref name="predicate"/>,
    /// or <see cref="PersistenceError.NotFound"/> if none match.
    /// </summary>
    Task<Result<T>> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// Returns all entities matching <paramref name="predicate"/>.
    /// Returns an empty list (not an error) when no entities match.
    /// </summary>
    Task<Result<IReadOnlyList<T>>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);

    /// <summary>
    /// Returns whether any entity matches <paramref name="predicate"/>.
    /// </summary>
    Task<Result<bool>> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// Returns the count of entities matching <paramref name="predicate"/>.
    /// </summary>
    Task<Result<int>> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
}
