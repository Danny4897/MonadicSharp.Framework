using MonadicSharp.Agents.Core;
using FluentAssertions;
using Xunit;

namespace MonadicSharp.Agents.Tests;

public class AgentContextTests
{
    [Fact]
    public void Require_WithSufficientCapabilities_ReturnsSuccess()
    {
        var ctx = AgentContext.Create(AgentCapability.ReadLocalFiles | AgentCapability.CallLlm);

        var result = ctx.Require(AgentCapability.ReadLocalFiles);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Require_WithMissingCapabilities_ReturnsFailure()
    {
        var ctx = AgentContext.Create(AgentCapability.ReadLocalFiles);

        var result = ctx.Require(AgentCapability.WriteLocalFiles);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AGENT_CAPABILITY_DENIED");
    }

    [Fact]
    public void Narrow_CannotEscalatePrivileges()
    {
        var ctx = AgentContext.Create(AgentCapability.ReadLocalFiles);

        // Try to get WriteLocalFiles via Narrow — should be silently dropped
        var narrowed = ctx.Narrow(AgentCapability.ReadLocalFiles | AgentCapability.WriteLocalFiles);

        narrowed.HasCapability(AgentCapability.WriteLocalFiles).Should().BeFalse();
        narrowed.HasCapability(AgentCapability.ReadLocalFiles).Should().BeTrue();
    }

    [Fact]
    public void Metadata_RoundTrip_Works()
    {
        var ctx = AgentContext.Create(AgentCapability.None)
            .WithMetadata("tenant", "acme-corp");

        var result = ctx.GetMetadata<string>("tenant");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("acme-corp");
    }

    [Fact]
    public void Metadata_WrongType_ReturnsTypedFailure()
    {
        var ctx = AgentContext.Create(AgentCapability.None)
            .WithMetadata("count", 42);

        var result = ctx.GetMetadata<string>("count");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AGENT_METADATA_TYPE_MISMATCH");
    }

    [Fact]
    public void Sandboxed_GrantsNoCapabilities()
    {
        var ctx = AgentContext.Sandboxed();

        ctx.HasCapability(AgentCapability.ReadLocalFiles).Should().BeFalse();
        ctx.HasCapability(AgentCapability.CallLlm).Should().BeFalse();
    }
}
