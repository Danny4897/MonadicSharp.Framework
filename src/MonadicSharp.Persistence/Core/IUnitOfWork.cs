namespace MonadicSharp.Persistence.Core;

/// <summary>
/// Result-aware Unit of Work: persists all tracked changes atomically.
/// <see cref="SaveChangesAsync"/> returns a typed <see cref="Result{T}"/> —
/// concurrency conflicts become <see cref="PersistenceError.ConcurrencyConflict"/>,
/// database errors become <see cref="PersistenceError.DatabaseError"/>,
/// never unhandled exceptions.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Persists all pending changes to the store.
    /// Returns the number of state entries written on success.
    /// </summary>
    Task<Result<int>> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begins a database transaction. Nested calls are not supported — returns an error
    /// if a transaction is already active.
    /// </summary>
    Task<Result<Unit>> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Commits the active transaction.
    /// Returns <see cref="PersistenceError.DatabaseError"/> if no transaction is active.
    /// </summary>
    Task<Result<Unit>> CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Rolls back the active transaction and discards all pending changes.
    /// Returns <see cref="PersistenceError.DatabaseError"/> if no transaction is active.
    /// </summary>
    Task<Result<Unit>> RollbackTransactionAsync(CancellationToken ct = default);
}
