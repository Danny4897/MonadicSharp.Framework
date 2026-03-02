using MonadicSharp.Http.Core;

namespace MonadicSharp.Http.Resilience;

/// <summary>
/// Exponential-backoff retry for Result-returning HTTP operations.
/// Only retries on transient errors (network failure, 429, 5xx); returns immediately
/// on client errors (4xx except 429) to avoid hammering a broken request.
/// </summary>
public sealed class RetryPolicy
{
    public int MaxAttempts { get; }
    public TimeSpan InitialDelay { get; }
    public double BackoffMultiplier { get; }
    public TimeSpan MaxDelay { get; }

    public static readonly RetryPolicy Default = new(maxAttempts: 3, initialDelay: TimeSpan.FromMilliseconds(200));
    public static readonly RetryPolicy None = new(maxAttempts: 1);

    public RetryPolicy(
        int maxAttempts = 3,
        TimeSpan? initialDelay = null,
        double backoffMultiplier = 2.0,
        TimeSpan? maxDelay = null)
    {
        MaxAttempts = maxAttempts;
        InitialDelay = initialDelay ?? TimeSpan.FromMilliseconds(200);
        BackoffMultiplier = backoffMultiplier;
        MaxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Executes <paramref name="operation"/> with retries.
    /// Returns <see cref="HttpError.RetryExhausted"/> after all attempts fail.
    /// </summary>
    public async Task<Result<T>> ExecuteAsync<T>(
        string url,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken ct = default)
    {
        var delay = InitialDelay;
        Error? lastError = null;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var result = await operation(ct).ConfigureAwait(false);
            if (result.IsSuccess)
                return result;

            lastError = result.Error;

            // Don't retry on non-transient client errors
            if (!IsTransient(result.Error) || attempt == MaxAttempts)
                break;

            await Task.Delay(delay, ct).ConfigureAwait(false);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * BackoffMultiplier, MaxDelay.TotalMilliseconds));
        }

        return Result<T>.Failure(
            MaxAttempts > 1
                ? HttpError.RetryExhausted(url, MaxAttempts, lastError!)
                : lastError!);
    }

    private static bool IsTransient(Error error) =>
        error.Code is "HTTP_NETWORK_FAILURE" or "HTTP_TIMEOUT" or "HTTP_RATE_LIMITED" or "HTTP_SERVER_ERROR";
}
