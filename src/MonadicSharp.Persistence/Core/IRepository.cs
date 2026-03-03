namespace MonadicSharp.Persistence.Core;

/// <summary>
/// Full read-write repository for a given entity type.
/// Extends <see cref="IReadRepository{T,TId}"/> with mutation operations,
/// all returning typed <see cref="Result{T}"/> values.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TId">The type of the entity's primary key.</typeparam>
public interface IRepository<T, TId> : IReadRepository<T, TId>
    where T : class
{
    /// <summary>
    /// Adds a new entity to the repository.
    /// Returns <see cref="PersistenceError.Conflict"/> if an entity with the same key exists.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    Task<Result<T>> AddAsync(T entity, CancellationToken ct = default);

    /// <summary>
    /// Adds multiple entities to the repository in a single operation.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    Task<Result<Unit>> AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>
    /// Begins tracking an existing entity for update.
    /// Returns <see cref="PersistenceError.NotFound"/> if the entity does not exist.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    Result<T> Update(T entity);

    /// <summary>
    /// Marks an entity for deletion by primary key.
    /// Returns <see cref="PersistenceError.NotFound"/> if no entity with that key exists.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    Task<Result<Unit>> DeleteAsync(TId id, CancellationToken ct = default);

    /// <summary>
    /// Marks an existing entity for deletion.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    Result<Unit> Delete(T entity);
}
