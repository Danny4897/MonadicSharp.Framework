using FluentAssertions;
using Xunit;

namespace MonadicSharp.Persistence.Tests;

public class PersistenceErrorTests
{
    [Fact]
    public void NotFound_ShouldHaveCorrectCode()
    {
        var error = Core.PersistenceError.NotFound("Order", 42);

        error.Code.Should().Be("PERSISTENCE_NOT_FOUND");
        error.Message.Should().Contain("Order");
        error.Message.Should().Contain("42");
        error.Metadata["EntityName"].Should().Be("Order");
        error.Metadata["EntityId"].Should().Be("42");
    }

    [Fact]
    public void Conflict_ShouldHaveCorrectCode()
    {
        var error = Core.PersistenceError.Conflict("User", "user-1");

        error.Code.Should().Be("PERSISTENCE_CONFLICT");
        error.Metadata["EntityName"].Should().Be("User");
        error.Metadata["EntityId"].Should().Be("user-1");
    }

    [Fact]
    public void ConcurrencyConflict_ShouldHaveCorrectCode()
    {
        var error = Core.PersistenceError.ConcurrencyConflict("Product", 99);

        error.Code.Should().Be("PERSISTENCE_CONCURRENCY_CONFLICT");
        error.Message.Should().Contain("modified by another process");
    }

    [Fact]
    public void DatabaseError_ShouldWrapException()
    {
        var ex = new InvalidOperationException("DB is down");
        var error = Core.PersistenceError.DatabaseError("SaveChanges", ex);

        error.Code.Should().Be("PERSISTENCE_DATABASE_ERROR");
        error.Metadata["DatabaseOperation"].Should().Be("SaveChanges");
    }

    [Fact]
    public void TooManyResults_ShouldIncludeCount()
    {
        var error = Core.PersistenceError.TooManyResults("Invoice", 5);

        error.Code.Should().Be("PERSISTENCE_TOO_MANY_RESULTS");
        error.Message.Should().Contain("5");
        error.Metadata["ResultCount"].Should().Be(5);
    }
}
