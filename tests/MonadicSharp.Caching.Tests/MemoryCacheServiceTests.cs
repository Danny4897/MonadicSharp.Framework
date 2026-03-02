using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using MonadicSharp.Caching.Core;
using MonadicSharp.Caching.Implementations;
using Xunit;

namespace MonadicSharp.Caching.Tests;

public class MemoryCacheServiceTests : IDisposable
{
    private readonly IMemoryCache _memCache = new MemoryCache(new MemoryCacheOptions());
    private MemoryCacheService Cache() => new(_memCache);

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_MissingKey_ReturnsCacheMissError()
    {
        var result = await Cache().GetAsync<string>("no-such-key");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CACHE_MISS");
    }

    [Fact]
    public async Task Get_AfterSet_ReturnsValue()
    {
        var svc = Cache();
        await svc.SetAsync("key", "hello");

        var result = await svc.GetAsync<string>("key");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public async Task Get_WrongType_ReturnsMiss()
    {
        var svc = Cache();
        await svc.SetAsync("key", 42);  // stored as int

        var result = await svc.GetAsync<string>("key"); // read as string

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CACHE_MISS");
    }

    // ── Set ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Set_ReturnsSuccess()
    {
        var result = await Cache().SetAsync("k", "v");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Set_OverwritesExistingKey()
    {
        var svc = Cache();
        await svc.SetAsync("key", "first");
        await svc.SetAsync("key", "second");

        var result = await svc.GetAsync<string>("key");
        result.Value.Should().Be("second");
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_ExistingKey_KeyDisappears()
    {
        var svc = Cache();
        await svc.SetAsync("key", "data");
        await svc.RemoveAsync("key");

        var result = await svc.GetAsync<string>("key");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Remove_MissingKey_ReturnsSuccess()
    {
        var result = await Cache().RemoveAsync("ghost");
        result.IsSuccess.Should().BeTrue();
    }

    // ── GetOrSet ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrSet_OnMiss_CallsFactory()
    {
        var factoryCalled = false;
        var result = await Cache().GetOrSetAsync<string>(
            "key",
            _ =>
            {
                factoryCalled = true;
                return Task.FromResult(Result<string>.Success("from-factory"));
            });

        factoryCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("from-factory");
    }

    [Fact]
    public async Task GetOrSet_OnHit_SkipsFactory()
    {
        var svc = Cache();
        await svc.SetAsync("key", "cached");

        var factoryCalled = false;
        var result = await svc.GetOrSetAsync<string>(
            "key",
            _ =>
            {
                factoryCalled = true;
                return Task.FromResult(Result<string>.Success("from-factory"));
            });

        factoryCalled.Should().BeFalse();
        result.Value.Should().Be("cached");
    }

    [Fact]
    public async Task GetOrSet_FactoryFailure_ReturnsFailureWithoutCaching()
    {
        var svc = Cache();
        var result = await svc.GetOrSetAsync<string>(
            "key",
            _ => Task.FromResult(Result<string>.Failure(Error.Create("Factory failed", "FACTORY_ERR"))));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FACTORY_ERR");

        // Verify nothing was stored
        var get = await svc.GetAsync<string>("key");
        get.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrSet_MissCallsFactory_ThenCachesResult()
    {
        var svc = Cache();
        int callCount = 0;

        await svc.GetOrSetAsync<string>("key", _ =>
        {
            callCount++;
            return Task.FromResult(Result<string>.Success("value"));
        });

        // Second call should hit cache
        await svc.GetOrSetAsync<string>("key", _ =>
        {
            callCount++;
            return Task.FromResult(Result<string>.Success("value"));
        });

        callCount.Should().Be(1);
    }

    public void Dispose() => _memCache.Dispose();
}
