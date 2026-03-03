using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonadicSharp.Persistence.Core;

namespace MonadicSharp.Persistence.Implementations;

/// <summary>
/// EF Core 8 implementation of <see cref="IUnitOfWork"/>.
/// Persists changes atomically. Concurrency conflicts and database errors
/// are translated to typed <see cref="PersistenceError"/> values.
/// </summary>
public sealed class EfCoreUnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private readonly ILogger<EfCoreUnitOfWork> _logger;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _transaction;

    public EfCoreUnitOfWork(DbContext context, ILogger<EfCoreUnitOfWork> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<int>> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            var count = await _context.SaveChangesAsync(ct);
            return Result<int>.Success(count);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityName = ex.Entries.FirstOrDefault()?.Entity.GetType().Name ?? "unknown";
            _logger.LogWarning(ex, "Concurrency conflict saving {Entity}", entityName);
            return Result<int>.Failure(PersistenceError.ConcurrencyConflict(entityName, "unknown"));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during SaveChangesAsync");
            return Result<int>.Failure(PersistenceError.DatabaseError(nameof(SaveChangesAsync), ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during SaveChangesAsync");
            return Result<int>.Failure(PersistenceError.DatabaseError(nameof(SaveChangesAsync), ex));
        }
    }

    public async Task<Result<Unit>> BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            return Result<Unit>.Failure(
                Error.Create("A transaction is already active.", "PERSISTENCE_TRANSACTION_ALREADY_ACTIVE"));

        try
        {
            _transaction = await _context.Database.BeginTransactionAsync(ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BeginTransactionAsync failed");
            return Result<Unit>.Failure(PersistenceError.DatabaseError(nameof(BeginTransactionAsync), ex));
        }
    }

    public async Task<Result<Unit>> CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            return Result<Unit>.Failure(
                Error.Create("No active transaction to commit.", "PERSISTENCE_NO_ACTIVE_TRANSACTION"));

        try
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CommitTransactionAsync failed");
            return Result<Unit>.Failure(PersistenceError.DatabaseError(nameof(CommitTransactionAsync), ex));
        }
    }

    public async Task<Result<Unit>> RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            return Result<Unit>.Failure(
                Error.Create("No active transaction to roll back.", "PERSISTENCE_NO_ACTIVE_TRANSACTION"));

        try
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RollbackTransactionAsync failed");
            return Result<Unit>.Failure(PersistenceError.DatabaseError(nameof(RollbackTransactionAsync), ex));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        await _context.DisposeAsync();
    }
}
