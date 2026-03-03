using FluentAssertions;
using Xunit;

namespace MonadicSharp.Persistence.Tests;

public class EfCoreRepositoryTests : IAsyncDisposable
{
    private readonly TestDbContext _context;
    private readonly Implementations.EfCoreRepository<TestProduct, int> _repository;
    private readonly Implementations.EfCoreUnitOfWork _uow;

    public EfCoreRepositoryTests()
    {
        _context = TestFactory.CreateDbContext();
        _repository = TestFactory.CreateRepository(_context);
        _uow = TestFactory.CreateUnitOfWork(_context);
    }

    // ── FindAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task FindAsync_WhenEntityExists_ReturnsSuccess()
    {
        _context.Products.Add(new TestProduct { Id = 1, Name = "Widget", Price = 9.99m });
        await _context.SaveChangesAsync();

        var result = await _repository.FindAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Widget");
    }

    [Fact]
    public async Task FindAsync_WhenEntityMissing_ReturnsNotFound()
    {
        var result = await _repository.FindAsync(999);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("PERSISTENCE_NOT_FOUND");
    }

    // ── FirstOrDefaultAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task FirstOrDefaultAsync_WhenMatches_ReturnsSuccess()
    {
        _context.Products.Add(new TestProduct { Id = 10, Name = "Gadget", Price = 49.99m });
        await _context.SaveChangesAsync();

        var result = await _repository.FirstOrDefaultAsync(p => p.Name == "Gadget");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Price.Should().Be(49.99m);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WhenNoMatch_ReturnsNotFound()
    {
        var result = await _repository.FirstOrDefaultAsync(p => p.Name == "DoesNotExist");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("PERSISTENCE_NOT_FOUND");
    }

    // ── ListAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_WithPredicate_ReturnsMatchingEntities()
    {
        _context.Products.AddRange(
            new TestProduct { Id = 20, Name = "Cheap", Price = 1m },
            new TestProduct { Id = 21, Name = "Expensive", Price = 100m },
            new TestProduct { Id = 22, Name = "AlsoCheap", Price = 2m });
        await _context.SaveChangesAsync();

        var result = await _repository.ListAsync(p => p.Price < 10m);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_WithoutPredicate_ReturnsAllEntities()
    {
        _context.Products.AddRange(
            new TestProduct { Id = 30, Name = "A", Price = 1m },
            new TestProduct { Id = 31, Name = "B", Price = 2m });
        await _context.SaveChangesAsync();

        var result = await _repository.ListAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_WhenNoMatch_ReturnsEmptyList()
    {
        var result = await _repository.ListAsync(p => p.Name == "nonexistent");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    // ── AnyAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnyAsync_WhenMatches_ReturnsTrue()
    {
        _context.Products.Add(new TestProduct { Id = 40, Name = "Present", Price = 1m });
        await _context.SaveChangesAsync();

        var result = await _repository.AnyAsync(p => p.Name == "Present");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task AnyAsync_WhenNoMatch_ReturnsFalse()
    {
        var result = await _repository.AnyAsync(p => p.Name == "Ghost");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    // ── CountAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        _context.Products.AddRange(
            new TestProduct { Id = 50, Name = "X", Price = 1m },
            new TestProduct { Id = 51, Name = "Y", Price = 2m });
        await _context.SaveChangesAsync();

        var result = await _repository.CountAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
    }

    // ── AddAsync + SaveChanges ────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ThenSaveChanges_PersistsEntity()
    {
        var product = new TestProduct { Id = 60, Name = "New", Price = 5m };

        var addResult = await _repository.AddAsync(product);
        addResult.IsSuccess.Should().BeTrue();

        var saveResult = await _uow.SaveChangesAsync();
        saveResult.IsSuccess.Should().BeTrue();
        saveResult.Value.Should().Be(1);

        var found = await _repository.FindAsync(60);
        found.IsSuccess.Should().BeTrue();
        found.Value!.Name.Should().Be("New");
    }

    // ── Update + SaveChanges ──────────────────────────────────────────────────

    [Fact]
    public async Task Update_ThenSaveChanges_UpdatesEntity()
    {
        _context.Products.Add(new TestProduct { Id = 70, Name = "Old", Price = 10m });
        await _context.SaveChangesAsync();

        var findResult = await _repository.FindAsync(70);
        findResult.Value!.Name = "Updated";

        var updateResult = _repository.Update(findResult.Value!);
        updateResult.IsSuccess.Should().BeTrue();

        var saveResult = await _uow.SaveChangesAsync();
        saveResult.IsSuccess.Should().BeTrue();

        var refetch = await _repository.FindAsync(70);
        refetch.Value!.Name.Should().Be("Updated");
    }

    // ── DeleteAsync + SaveChanges ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingEntity_RemovesIt()
    {
        _context.Products.Add(new TestProduct { Id = 80, Name = "ToDelete", Price = 1m });
        await _context.SaveChangesAsync();

        var deleteResult = await _repository.DeleteAsync(80);
        deleteResult.IsSuccess.Should().BeTrue();

        await _uow.SaveChangesAsync();

        var findResult = await _repository.FindAsync(80);
        findResult.IsSuccess.Should().BeFalse();
        findResult.Error!.Code.Should().Be("PERSISTENCE_NOT_FOUND");
    }

    [Fact]
    public async Task DeleteAsync_MissingEntity_ReturnsNotFound()
    {
        var result = await _repository.DeleteAsync(9999);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("PERSISTENCE_NOT_FOUND");
    }

    // ── AddRangeAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddRangeAsync_ThenSaveChanges_PersistsAll()
    {
        var products = new[]
        {
            new TestProduct { Id = 90, Name = "Batch1", Price = 1m },
            new TestProduct { Id = 91, Name = "Batch2", Price = 2m }
        };

        var addResult = await _repository.AddRangeAsync(products);
        addResult.IsSuccess.Should().BeTrue();

        await _uow.SaveChangesAsync();

        var countResult = await _repository.CountAsync();
        countResult.Value.Should().Be(2);
    }

    public async ValueTask DisposeAsync()
    {
        await _uow.DisposeAsync();
    }
}
