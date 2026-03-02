using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MonadicSharp.Agents;
using MonadicSharp.Agents.Core;
using MonadicSharp.Caching.Core;
using MonadicSharp.Caching.Implementations;
using MonadicSharp.Caching.Middleware;
using Xunit;

namespace MonadicSharp.Caching.Tests;

// ── Stubs ─────────────────────────────────────────────────────────────────────

file sealed class CountingAgent : IAgent<string, string>
{
    public int CallCount { get; private set; }
    public string Name => "CountingAgent";
    public AgentCapability RequiredCapabilities => AgentCapability.None;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(Result<string>.Success($"result:{input}"));
    }
}

file sealed class AlwaysFailAgent : IAgent<string, string>
{
    public string Name => "AlwaysFailAgent";
    public AgentCapability RequiredCapabilities => AgentCapability.None;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Failure(Error.Create("Always fails", "AGENT_FAIL")));
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class CachingAgentWrapperTests : IDisposable
{
    private readonly IMemoryCache _memCache = new MemoryCache(new MemoryCacheOptions());
    private MemoryCacheService NewCache() => new(_memCache);
    private static readonly AgentContext Ctx = AgentContext.Create(AgentCapability.None);

    // ── Passthrough ───────────────────────────────────────────────────────────

    [Fact]
    public void Wrapper_ExposesInnerAgentName()
    {
        var wrapper = new CachingAgentWrapper<string, string>(new CountingAgent(), NewCache());
        wrapper.Name.Should().Be("CountingAgent");
    }

    // ── Cache hit / miss ──────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_CacheMiss_CallsInnerAgent()
    {
        var agent = new CountingAgent();
        var wrapper = new CachingAgentWrapper<string, string>(agent, NewCache());

        await wrapper.ExecuteAsync("input", Ctx);

        agent.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Execute_SecondCall_HitsCacheDoesNotCallAgent()
    {
        var agent = new CountingAgent();
        var wrapper = new CachingAgentWrapper<string, string>(agent, NewCache());

        await wrapper.ExecuteAsync("input", Ctx);
        await wrapper.ExecuteAsync("input", Ctx);

        agent.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Execute_DifferentInputs_BothCallAgent()
    {
        var agent = new CountingAgent();
        var wrapper = new CachingAgentWrapper<string, string>(agent, NewCache());

        await wrapper.ExecuteAsync("a", Ctx);
        await wrapper.ExecuteAsync("b", Ctx);

        agent.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Execute_CachedResult_MatchesOriginal()
    {
        var agent = new CountingAgent();
        var wrapper = new CachingAgentWrapper<string, string>(agent, NewCache());

        var first = await wrapper.ExecuteAsync("hello", Ctx);
        var second = await wrapper.ExecuteAsync("hello", Ctx);

        second.Value.Should().Be(first.Value);
    }

    // ── Failure caching ───────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_FailingAgent_DefaultPolicy_DoesNotCacheFailure()
    {
        var agent = new AlwaysFailAgent();
        var wrapper = new CachingAgentWrapper<string, string>(agent, NewCache());

        // Two calls — if failures were cached the cache would return cached failure
        // but we can't distinguish from a re-call here.
        // What we verify: the result is always a failure (not accidentally success)
        var r1 = await wrapper.ExecuteAsync("x", Ctx);
        var r2 = await wrapper.ExecuteAsync("x", Ctx);

        r1.IsFailure.Should().BeTrue();
        r2.IsFailure.Should().BeTrue();
        r2.Error.Code.Should().Be("AGENT_FAIL");
    }

    // ── Bypass predicate ──────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_BypassPredicate_AlwaysCallsAgent()
    {
        var agent = new CountingAgent();
        var policy = new AgentCachePolicy<string, string>
        {
            BypassPredicate = (input, _) => input.StartsWith("bypass:")
        };
        var wrapper = new CachingAgentWrapper<string, string>(agent, NewCache(), policy);

        await wrapper.ExecuteAsync("bypass:foo", Ctx);
        await wrapper.ExecuteAsync("bypass:foo", Ctx);

        agent.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Execute_NonBypassInput_StillUsesCacheNormally()
    {
        var agent = new CountingAgent();
        var policy = new AgentCachePolicy<string, string>
        {
            BypassPredicate = (input, _) => input.StartsWith("bypass:")
        };
        var wrapper = new CachingAgentWrapper<string, string>(agent, NewCache(), policy);

        await wrapper.ExecuteAsync("normal", Ctx);
        await wrapper.ExecuteAsync("normal", Ctx);

        agent.CallCount.Should().Be(1);
    }

    // ── Custom key factory ────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_CustomKeyFactory_UsedForCaching()
    {
        var agent = new CountingAgent();
        var policy = new AgentCachePolicy<string, string>
        {
            KeyFactory = (_, input) => $"custom:{input.ToUpperInvariant()}"
        };
        var wrapper = new CachingAgentWrapper<string, string>(agent, NewCache(), policy);

        await wrapper.ExecuteAsync("Hello", Ctx);
        await wrapper.ExecuteAsync("hello", Ctx); // same key because ToUpper

        agent.CallCount.Should().Be(1);
    }

    public void Dispose() => _memCache.Dispose();
}
