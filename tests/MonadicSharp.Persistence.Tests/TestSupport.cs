using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MonadicSharp.Persistence.Implementations;

namespace MonadicSharp.Persistence.Tests;

// ── Test entity ───────────────────────────────────────────────────────────────

public class TestProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// ── InMemory DbContext ────────────────────────────────────────────────────────

public class TestDbContext : DbContext
{
    public DbSet<TestProduct> Products => Set<TestProduct>();

    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestProduct>().HasKey(p => p.Id);
    }
}

// ── Factory helpers ───────────────────────────────────────────────────────────

public static class TestFactory
{
    public static TestDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new TestDbContext(options);
    }

    public static EfCoreRepository<TestProduct, int> CreateRepository(TestDbContext context) =>
        new(context, NullLogger<EfCoreRepository<TestProduct, int>>.Instance);

    public static EfCoreUnitOfWork CreateUnitOfWork(TestDbContext context) =>
        new(context, NullLogger<EfCoreUnitOfWork>.Instance);
}
