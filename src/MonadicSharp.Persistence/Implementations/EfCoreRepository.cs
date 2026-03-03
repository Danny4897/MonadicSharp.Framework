using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonadicSharp.Persistence.Core;

namespace MonadicSharp.Persistence.Implementations;

/// <summary>
/// EF Core 8 implementation of <see cref="IRepository{T,TId}"/>.
/// All operations wrap EF Core calls and translate exceptions into typed
/// <see cref="PersistenceError"/> values — no unhandled exceptions escape.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TId">The type of the entity's primary key.</typeparam>
public class EfCoreRepository<T, TId> : IRepository<T, TId>
    where T : class
{
    private readonly DbContext _context;
    private readonly DbSet<T> _set;
    private readonly ILogger<EfCoreRepository<T, TId>> _logger;
    private readonly string _entityName = typeof(T).Name;

    public EfCoreRepository(DbContext context, ILogger<EfCoreRepository<T, TId>> logger)
    {
        _context = context;
        _set = context.Set<T>();
        _logger = logger;
    }

    // ── Read operations ───────────────────────────────────────────────────────

    public async Task<Result<T>> FindAsync(TId id, CancellationToken ct = default)
    {
        try
        {
            var entity = await _set.FindAsync([id], ct);
            return entity is not null
                ? Result<T>.Success(entity)
                : Result<T>.Failure(PersistenceError.NotFound(_entityName, id!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FindAsync failed for {Entity} id={Id}", _entityName, id);
            return Result<T>.Failure(PersistenceError.DatabaseError(nameof(FindAsync), ex));
        }
    }

    public async Task<Result<T>> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        try
        {
            var entity = await _set.FirstOrDefaultAsync(predicate, ct);
            return entity is not null
                ? Result<T>.Success(entity)
                : Result<T>.Failure(PersistenceError.NotFound(_entityName, "predicate"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FirstOrDefaultAsync failed for {Entity}", _entityName);
            return Result<T>.Failure(PersistenceError.DatabaseError(nameof(FirstOrDefaultAsync), ex));
        }
    }

    public async Task<Result<IReadOnlyList<T>>> ListAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = predicate is not null ? _set.Where(predicate) : _set;
            var list = await query.ToListAsync(ct);
            return Result<IReadOnlyList<T>>.Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListAsync failed for {Entity}", _entityName);
            return Result<IReadOnlyList<T>>.Failure(PersistenceError.DatabaseError(nameof(ListAsync), ex));
        }
    }

    public async Task<Result<bool>> AnyAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _set.AnyAsync(predicate, ct);
            return Result<bool>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AnyAsync failed for {Entity}", _entityName);
            return Result<bool>.Failure(PersistenceError.DatabaseError(nameof(AnyAsync), ex));
        }
    }

    public async Task<Result<int>> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        try
        {
            var count = predicate is not null
                ? await _set.CountAsync(predicate, ct)
                : await _set.CountAsync(ct);
            return Result<int>.Success(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CountAsync failed for {Entity}", _entityName);
            return Result<int>.Failure(PersistenceError.DatabaseError(nameof(CountAsync), ex));
        }
    }

    // ── Write operations ──────────────────────────────────────────────────────

    public async Task<Result<T>> AddAsync(T entity, CancellationToken ct = default)
    {
        try
        {
            await _set.AddAsync(entity, ct);
            return Result<T>.Success(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddAsync failed for {Entity}", _entityName);
            return Result<T>.Failure(PersistenceError.DatabaseError(nameof(AddAsync), ex));
        }
    }

    public async Task<Result<Unit>> AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        try
        {
            await _set.AddRangeAsync(entities, ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddRangeAsync failed for {Entity}", _entityName);
            return Result<Unit>.Failure(PersistenceError.DatabaseError(nameof(AddRangeAsync), ex));
        }
    }

    public Result<T> Update(T entity)
    {
        try
        {
            _set.Update(entity);
            return Result<T>.Success(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed for {Entity}", _entityName);
            return Result<T>.Failure(PersistenceError.DatabaseError(nameof(Update), ex));
        }
    }

    public async Task<Result<Unit>> DeleteAsync(TId id, CancellationToken ct = default)
    {
        var findResult = await FindAsync(id, ct);
        return findResult.IsSuccess
            ? Delete(findResult.Value!)
            : Result<Unit>.Failure(findResult.Error!);
    }

    public Result<Unit> Delete(T entity)
    {
        try
        {
            _set.Remove(entity);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete failed for {Entity}", _entityName);
            return Result<Unit>.Failure(PersistenceError.DatabaseError(nameof(Delete), ex));
        }
    }
}
