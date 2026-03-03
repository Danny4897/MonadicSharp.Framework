using FluentAssertions;
using MonadicSharp.Persistence.Extensions;
using Xunit;

namespace MonadicSharp.Persistence.Tests;

public class QueryableExtensionsTests : IAsyncDisposable
{
    private readonly TestDbContext _context;

    public QueryableExtensionsTests()
    {
        _context = TestFactory.CreateDbContext();
        _context.Products.AddRange(
            new TestProduct { Id = 1, Name = "Alpha", Price = 10m },
            new TestProduct { Id = 2, Name = "Beta", Price = 20m },
            new TestProduct { Id = 3, Name = "Gamma", Price = 30m });
        _context.SaveChanges();
    }

    // ── ToResultAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ToResultAsync_ReturnsAllEntities()
    {
        var result = await _context.Products.ToResultAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
    }

    [Fact]
    public async Task ToResultAsync_WithLinqFilter_ReturnsFiltered()
    {
        var result = await _context.Products
            .Where(p => p.Price > 15m)
            .ToResultAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    // ── FirstOrDefaultResultAsync ─────────────────────────────────────────────

    [Fact]
    public async Task FirstOrDefaultResultAsync_WithPredicate_WhenExists_ReturnsSuccess()
    {
        var result = await _context.Products.FirstOrDefaultResultAsync(p => p.Name == "Beta");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Price.Should().Be(20m);
    }

    [Fact]
    public async Task FirstOrDefaultResultAsync_WithPredicate_WhenMissing_ReturnsNotFound()
    {
        var result = await _context.Products.FirstOrDefaultResultAsync(p => p.Name == "Missing");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("PERSISTENCE_NOT_FOUND");
    }

    [Fact]
    public async Task FirstOrDefaultResultAsync_NoPredicate_ReturnsFirst()
    {
        var result = await _context.Products
            .OrderBy(p => p.Id)
            .FirstOrDefaultResultAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Alpha");
    }

    // ── SingleResultAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SingleResultAsync_WhenExactlyOne_ReturnsSuccess()
    {
        var result = await _context.Products.SingleResultAsync(p => p.Name == "Alpha");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(1);
    }

    [Fact]
    public async Task SingleResultAsync_WhenNone_ReturnsNotFound()
    {
        var result = await _context.Products.SingleResultAsync(p => p.Name == "Ghost");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("PERSISTENCE_NOT_FOUND");
    }

    [Fact]
    public async Task SingleResultAsync_WhenMultiple_ReturnsTooManyResults()
    {
        var result = await _context.Products.SingleResultAsync(p => p.Price >= 10m);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("PERSISTENCE_TOO_MANY_RESULTS");
    }

    // ── CountResultAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CountResultAsync_ReturnsCorrectCount()
    {
        var result = await _context.Products.CountResultAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);
    }

    [Fact]
    public async Task CountResultAsync_WithLinqFilter_ReturnsFilteredCount()
    {
        var result = await _context.Products
            .Where(p => p.Price > 15m)
            .CountResultAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}
