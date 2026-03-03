# MonadicSharp.Persistence

> Result-aware repository and Unit of Work for EF Core 8 — NotFound, Conflict, and DatabaseError are typed `Result<T>` values, never null or unhandled exceptions.

[![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Persistence)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)

---

## Overview

`MonadicSharp.Persistence` brings Railway-Oriented Programming to the data access layer:

- **`IRepository<T, TId>`** — full read-write repository, all methods return `Result<T>`
- **`IReadRepository<T, TId>`** — read-only projection for CQRS-style separation
- **`IUnitOfWork`** — `SaveChangesAsync` returns `Result<int>`, concurrency conflicts are typed errors
- **`EfCoreRepository<T, TId>`** — EF Core 8 implementation
- **`EfCoreUnitOfWork`** — supports transactions via `BeginTransactionAsync` / `CommitTransactionAsync`
- **`QueryableExtensions`** — `.ToResultAsync()`, `.FirstOrDefaultResultAsync()`, `.SingleResultAsync()` for inline LINQ
- **`PersistenceError`** — typed error factory for all persistence failure scenarios

---

## Installation

```bash
dotnet add package MonadicSharp.Persistence
dotnet add package Microsoft.EntityFrameworkCore.SqlServer  # or Npgsql, Sqlite, etc.
```

---

## Quick Start

### 1. Define your DbContext as usual

```csharp
public class AppDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
    // ...
}
```

### 2. Register in DI

```csharp
services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(connectionString));

services
    .AddMonadicSharpPersistence<AppDbContext>()          // registers IUnitOfWork
    .AddMonadicSharpRepository<Order, Guid>();            // registers IRepository<Order, Guid>
```

### 3. Use in your services

```csharp
public class OrderService(IRepository<Order, Guid> orders, IUnitOfWork uow)
{
    public async Task<Result<Order>> PlaceOrderAsync(CreateOrderDto dto)
    {
        var order = new Order(dto.CustomerId, dto.Items);

        var addResult = await orders.AddAsync(order);
        if (addResult.IsFailure) return addResult;

        var saveResult = await uow.SaveChangesAsync();
        return saveResult.IsSuccess
            ? Result<Order>.Success(order)
            : Result<Order>.Failure(saveResult.Error);
    }

    public async Task<Result<Order>> GetOrderAsync(Guid id) =>
        await orders.FindAsync(id);
    // Returns PersistenceError.NotFound if missing — no null checks needed
}
```

---

## IRepository API

```csharp
// Read
Task<Result<T>>                 FindAsync(TId id, ct)
Task<Result<T>>                 FirstOrDefaultAsync(predicate, ct)
Task<Result<IReadOnlyList<T>>>  ListAsync(predicate?, ct)
Task<Result<bool>>              AnyAsync(predicate, ct)
Task<Result<int>>               CountAsync(predicate?, ct)

// Write (changes not persisted until SaveChangesAsync)
Task<Result<T>>     AddAsync(T entity, ct)
Task<Result<Unit>>  AddRangeAsync(IEnumerable<T> entities, ct)
Result<T>           Update(T entity)
Task<Result<Unit>>  DeleteAsync(TId id, ct)
Result<Unit>        Delete(T entity)
```

---

## IUnitOfWork API

```csharp
Task<Result<int>>   SaveChangesAsync(ct)             // persists all pending changes
Task<Result<Unit>>  BeginTransactionAsync(ct)
Task<Result<Unit>>  CommitTransactionAsync(ct)
Task<Result<Unit>>  RollbackTransactionAsync(ct)
```

### Transaction example

```csharp
await uow.BeginTransactionAsync();

var addOrder = await orders.AddAsync(order);
var debitStock = await inventory.UpdateAsync(item);

if (addOrder.IsFailure || debitStock.IsFailure)
{
    await uow.RollbackTransactionAsync();
    return Result<Unit>.Failure(addOrder.Error ?? debitStock.Error!);
}

await uow.SaveChangesAsync();
await uow.CommitTransactionAsync();
```

---

## QueryableExtensions

For custom queries that go beyond the repository interface:

```csharp
var recentOrders = await dbContext.Orders
    .Where(o => o.CreatedAt > DateTime.UtcNow.AddDays(-7))
    .OrderByDescending(o => o.CreatedAt)
    .ToResultAsync(ct);

// Single entity
var order = await dbContext.Orders
    .SingleResultAsync(o => o.ExternalRef == externalId, ct);
// Returns PersistenceError.NotFound or PersistenceError.TooManyResults

// First or not-found
var latest = await dbContext.Orders
    .OrderByDescending(o => o.CreatedAt)
    .FirstOrDefaultResultAsync(ct);
```

---

## Error Codes

| Code | Meaning |
|------|---------|
| `PERSISTENCE_NOT_FOUND` | Entity with the given id/predicate does not exist |
| `PERSISTENCE_CONFLICT` | Entity with the same key already exists |
| `PERSISTENCE_CONCURRENCY_CONFLICT` | `DbUpdateConcurrencyException` — row was modified by another process |
| `PERSISTENCE_DATABASE_ERROR` | Unexpected database exception |
| `PERSISTENCE_TOO_MANY_RESULTS` | `SingleResultAsync` found more than one match |
| `PERSISTENCE_TRANSACTION_ALREADY_ACTIVE` | `BeginTransactionAsync` called twice |
| `PERSISTENCE_NO_ACTIVE_TRANSACTION` | Commit/Rollback without Begin |

---

## License

MIT — part of [MonadicSharp.Framework](../../README.md).
