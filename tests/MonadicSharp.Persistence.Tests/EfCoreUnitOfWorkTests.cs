using FluentAssertions;
using Xunit;

namespace MonadicSharp.Persistence.Tests;

public class EfCoreUnitOfWorkTests : IAsyncDisposable
{
    private readonly TestDbContext _context;
    private readonly Implementations.EfCoreRepository<TestProduct, int> _repository;
    private readonly Implementations.EfCoreUnitOfWork _uow;

    public EfCoreUnitOfWorkTests()
    {
        _context = TestFactory.CreateDbContext();
        _repository = TestFactory.CreateRepository(_context);
        _uow = TestFactory.CreateUnitOfWork(_context);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoChanges_ReturnsZero()
    {
        var result = await _uow.SaveChangesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task SaveChangesAsync_WithPendingAdd_ReturnsOne()
    {
        await _repository.AddAsync(new TestProduct { Id = 1, Name = "Product", Price = 10m });

        var result = await _uow.SaveChangesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task BeginTransaction_ThenCommit_Succeeds()
    {
        var beginResult = await _uow.BeginTransactionAsync();
        beginResult.IsSuccess.Should().BeTrue();

        await _repository.AddAsync(new TestProduct { Id = 2, Name = "Transacted", Price = 5m });
        await _uow.SaveChangesAsync();

        var commitResult = await _uow.CommitTransactionAsync();
        commitResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task BeginTransaction_Twice_ReturnsError()
    {
        await _uow.BeginTransactionAsync();

        var secondBegin = await _uow.BeginTransactionAsync();

        secondBegin.IsSuccess.Should().BeFalse();
        secondBegin.Error!.Code.Should().Be("PERSISTENCE_TRANSACTION_ALREADY_ACTIVE");
    }

    [Fact]
    public async Task CommitTransaction_WithoutBegin_ReturnsError()
    {
        var result = await _uow.CommitTransactionAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("PERSISTENCE_NO_ACTIVE_TRANSACTION");
    }

    [Fact]
    public async Task RollbackTransaction_WithoutBegin_ReturnsError()
    {
        var result = await _uow.RollbackTransactionAsync();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("PERSISTENCE_NO_ACTIVE_TRANSACTION");
    }

    [Fact]
    public async Task BeginTransaction_ThenRollback_DiscardsChanges()
    {
        await _uow.BeginTransactionAsync();

        await _repository.AddAsync(new TestProduct { Id = 3, Name = "Rollback", Price = 1m });
        await _uow.SaveChangesAsync();

        var rollbackResult = await _uow.RollbackTransactionAsync();
        rollbackResult.IsSuccess.Should().BeTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await _uow.DisposeAsync();
    }
}
