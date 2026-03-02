using FluentAssertions;
using MonadicSharp.Http.Resilience;
using Xunit;

namespace MonadicSharp.Http.Tests;

public class RetryPolicyTests
{
    [Fact]
    public async Task Execute_SuccessOnFirstAttempt_NeverRetries()
    {
        int calls = 0;
        var policy = new RetryPolicy(maxAttempts: 3, initialDelay: TimeSpan.FromMilliseconds(1));

        var result = await policy.ExecuteAsync<string>("http://x", _ =>
        {
            calls++;
            return Task.FromResult(Result<string>.Success("ok"));
        });

        result.IsSuccess.Should().BeTrue();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Execute_TransientFailureThenSuccess_RetriesAndSucceeds()
    {
        int calls = 0;
        var policy = new RetryPolicy(maxAttempts: 3, initialDelay: TimeSpan.FromMilliseconds(1));

        var result = await policy.ExecuteAsync<string>("http://x", _ =>
        {
            calls++;
            return calls < 3
                ? Task.FromResult(Result<string>.Failure(Error.Create("network", "HTTP_NETWORK_FAILURE")))
                : Task.FromResult(Result<string>.Success("ok"));
        });

        result.IsSuccess.Should().BeTrue();
        calls.Should().Be(3);
    }

    [Fact]
    public async Task Execute_AllAttemptsTransientFail_ReturnsRetryExhausted()
    {
        var policy = new RetryPolicy(maxAttempts: 3, initialDelay: TimeSpan.FromMilliseconds(1));

        var result = await policy.ExecuteAsync<string>("http://x", _ =>
            Task.FromResult(Result<string>.Failure(Error.Create("server error", "HTTP_SERVER_ERROR"))));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HTTP_RETRY_EXHAUSTED");
    }

    [Fact]
    public async Task Execute_NonTransientError_DoesNotRetry()
    {
        int calls = 0;
        var policy = new RetryPolicy(maxAttempts: 3, initialDelay: TimeSpan.FromMilliseconds(1));

        var result = await policy.ExecuteAsync<string>("http://x", _ =>
        {
            calls++;
            return Task.FromResult(Result<string>.Failure(Error.Create("not found", "HTTP_NOT_FOUND")));
        });

        result.IsFailure.Should().BeTrue();
        calls.Should().Be(1); // no retry on 404
    }

    [Fact]
    public async Task Execute_SingleAttemptPolicy_NoRetryExhaustedWrapping()
    {
        var policy = RetryPolicy.None;

        var result = await policy.ExecuteAsync<string>("http://x", _ =>
            Task.FromResult(Result<string>.Failure(Error.Create("fail", "HTTP_SERVER_ERROR"))));

        result.Error.Code.Should().Be("HTTP_SERVER_ERROR"); // not wrapped
    }
}
