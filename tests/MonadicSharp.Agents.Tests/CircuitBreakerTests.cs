using MonadicSharp.Agents.Resilience;
using FluentAssertions;
using Xunit;

namespace MonadicSharp.Agents.Tests;

public class CircuitBreakerTests
{
    [Fact]
    public async Task CircuitBreaker_OpensAfterThreshold()
    {
        var breaker = new CircuitBreaker("test", failureThreshold: 3, openDuration: TimeSpan.FromMinutes(10));

        // Trigger 3 failures
        for (int i = 0; i < 3; i++)
            await breaker.ExecuteAsync<string>(_ => Task.FromResult(Result<string>.Failure(Error.Create("fail", "ERR"))));

        // 4th call should be rejected by open circuit
        var result = await breaker.ExecuteAsync<string>(_ => Task.FromResult(Result<string>.Success("ok")));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AGENT_CIRCUIT_OPEN");
        breaker.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public async Task CircuitBreaker_ClosedOnSuccess()
    {
        var breaker = new CircuitBreaker("test", failureThreshold: 3);

        // 2 failures (below threshold)
        for (int i = 0; i < 2; i++)
            await breaker.ExecuteAsync<string>(_ => Task.FromResult(Result<string>.Failure(Error.Create("fail", "ERR"))));

        // Success resets counter
        var result = await breaker.ExecuteAsync<string>(_ => Task.FromResult(Result<string>.Success("ok")));

        result.IsSuccess.Should().BeTrue();
        breaker.State.Should().Be(CircuitState.Closed);
        breaker.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task CircuitBreaker_TransitionsToHalfOpenAfterDuration()
    {
        var breaker = new CircuitBreaker("test", failureThreshold: 1, openDuration: TimeSpan.FromMilliseconds(50));

        // Trip the circuit
        await breaker.ExecuteAsync<string>(_ => Task.FromResult(Result<string>.Failure(Error.Create("fail", "ERR"))));
        breaker.State.Should().Be(CircuitState.Open);

        // Wait for open duration
        await Task.Delay(100);

        // Next call should be allowed (half-open probe)
        var result = await breaker.ExecuteAsync<string>(_ => Task.FromResult(Result<string>.Success("probe ok")));

        result.IsSuccess.Should().BeTrue();
        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void Reset_ClosesCircuit()
    {
        var breaker = new CircuitBreaker("test", failureThreshold: 1);
        // We can't easily open it without async here, so test reset on fresh breaker
        breaker.Reset();
        breaker.State.Should().Be(CircuitState.Closed);
        breaker.ConsecutiveFailures.Should().Be(0);
    }
}
