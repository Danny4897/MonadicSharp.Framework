using MonadicSharp.Agents.Core;
using MonadicSharp.Agents.Pipeline;
using FluentAssertions;
using Xunit;

namespace MonadicSharp.Agents.Tests;

// ── Fake agents for testing ───────────────────────────────────────────────────

file sealed class UpperCaseAgent : IAgent<string, string>
{
    public string Name => "UpperCase";
    public AgentCapability RequiredCapabilities => AgentCapability.None;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Success(input.ToUpperInvariant()));
}

file sealed class TrimAgent : IAgent<string, string>
{
    public string Name => "Trim";
    public AgentCapability RequiredCapabilities => AgentCapability.None;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Success(input.Trim()));
}

file sealed class FailingAgent : IAgent<string, string>
{
    public string Name => "Failing";
    public AgentCapability RequiredCapabilities => AgentCapability.None;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Failure(Error.Create("Intentional failure", "TEST_FAIL")));
}

file sealed class RestrictedAgent : IAgent<string, string>
{
    public string Name => "Restricted";
    public AgentCapability RequiredCapabilities => AgentCapability.WriteLocalFiles;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Success(input));
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class AgentPipelineTests
{
    private static AgentContext DefaultContext => AgentContext.Create(AgentCapability.None);

    [Fact]
    public async Task SequentialPipeline_HappyPath_ProducesExpectedOutput()
    {
        var pipeline = AgentPipeline
            .Start<string, string>("Test", new TrimAgent())
            .Then(new UpperCaseAgent());

        var result = await pipeline.RunAsync("  hello world  ", DefaultContext);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("HELLO WORLD");
    }

    [Fact]
    public async Task SequentialPipeline_ShortCircuitsOnFailure()
    {
        var pipeline = AgentPipeline
            .Start<string, string>("Test", new FailingAgent())
            .Then(new UpperCaseAgent()); // should NOT run

        var result = await pipeline.RunAsync("input", DefaultContext);

        result.IsFailure.Should().BeTrue();
        // Only one step should have executed
        result.Steps.Should().HaveCount(1);
        result.Steps[0].AgentName.Should().Be("Failing");
    }

    [Fact]
    public async Task SequentialPipeline_TracksAllSteps()
    {
        var pipeline = AgentPipeline
            .Start<string, string>("Test", new TrimAgent())
            .Then(new UpperCaseAgent());

        var result = await pipeline.RunAsync("  hello  ", DefaultContext);

        result.Steps.Should().HaveCount(2);
        result.Steps.Should().AllSatisfy(s => s.Succeeded.Should().BeTrue());
    }

    [Fact]
    public async Task SequentialPipeline_BlocksAgentWithInsufficientCapabilities()
    {
        var ctx = AgentContext.Create(AgentCapability.None); // No WriteLocalFiles
        var pipeline = AgentPipeline.Start<string, string>("Test", new RestrictedAgent());

        var result = await pipeline.RunAsync("input", ctx);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AGENT_AUTHORIZATION_FAILED");
    }

    [Fact]
    public async Task ParallelPipeline_AllSucceed_ReturnsAllResults()
    {
        var pipeline = ParallelAgentPipeline.Of<string, string>(
            "Parallel",
            new TrimAgent(),
            new UpperCaseAgent());

        var result = await pipeline.RunAllAsync("  test  ", DefaultContext);

        result.AllSucceeded.Should().BeTrue();
        result.Outcomes.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParallelPipeline_PartialFailure_ReportsCorrectly()
    {
        var pipeline = ParallelAgentPipeline.Of<string, string>(
            "Parallel",
            new TrimAgent(),
            new FailingAgent());

        var result = await pipeline.RunAllAsync("test", DefaultContext);

        result.HasFailures.Should().BeTrue();
        result.AllSucceeded.Should().BeFalse();
        result.Failures.Should().HaveCount(1);
        result.Successes.Should().HaveCount(1);
    }
}
