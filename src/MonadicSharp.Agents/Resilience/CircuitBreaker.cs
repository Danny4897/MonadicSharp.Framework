using MonadicSharp.Agents.Errors;

namespace MonadicSharp.Agents.Resilience;

/// <summary>
/// Prevents cascading failures in multi-agent systems by tracking failure rates
/// and temporarily blocking calls when a downstream service is unhealthy.
///
/// States:
/// - CLOSED — normal operation; failures are counted
/// - OPEN   — blocking all calls; trips after <see cref="FailureThreshold"/> consecutive failures
/// - HALF-OPEN — allows one probe call after <see cref="OpenDuration"/>; closes on success, re-opens on failure
///
/// All state transitions return <see cref="Result{T}"/> — no exceptions leak.
/// Thread-safe via lock-free atomic operations where possible.
/// </summary>
public sealed class CircuitBreaker
{
    private readonly string _name;
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly TimeSpan _halfOpenTimeout;

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;
    private int _halfOpenProbeInFlight;

    public string Name => _name;
    public CircuitState State => _state;
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <param name="name">Identifier used in error messages and logs.</param>
    /// <param name="failureThreshold">Consecutive failures before the circuit opens.</param>
    /// <param name="openDuration">How long the circuit stays open before allowing a probe.</param>
    /// <param name="halfOpenTimeout">Max time a probe call may take before being considered failed.</param>
    public CircuitBreaker(
        string name,
        int failureThreshold = 5,
        TimeSpan? openDuration = null,
        TimeSpan? halfOpenTimeout = null)
    {
        _name = name;
        _failureThreshold = failureThreshold;
        _openDuration = openDuration ?? TimeSpan.FromSeconds(30);
        _halfOpenTimeout = halfOpenTimeout ?? TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Executes <paramref name="operation"/> through the circuit breaker.
    /// Returns <see cref="AgentError.CircuitOpen"/> without calling the operation when the circuit is OPEN.
    /// </summary>
    public async Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        var (canProceed, preCheckError) = CheckState();
        if (!canProceed)
            return Result<T>.Failure(preCheckError!);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_state == CircuitState.HalfOpen)
                timeoutCts.CancelAfter(_halfOpenTimeout);

            var result = await operation(timeoutCts.Token).ConfigureAwait(false);

            if (result.IsSuccess)
                OnSuccess();
            else
                OnFailure();

            return result;
        }
        catch (OperationCanceledException) when (_state == CircuitState.HalfOpen)
        {
            OnFailure();
            return Result<T>.Failure(AgentError.Timeout(_name, _halfOpenTimeout));
        }
        catch (Exception ex)
        {
            OnFailure();
            return Result<T>.Failure(Error.FromException(ex, "CIRCUIT_UNHANDLED").WithMetadata("Circuit", _name));
        }
        finally
        {
            if (_state == CircuitState.HalfOpen)
                Interlocked.Exchange(ref _halfOpenProbeInFlight, 0);
        }
    }

    private (bool canProceed, Error? error) CheckState()
    {
        if (_state == CircuitState.Closed)
            return (true, null);

        if (_state == CircuitState.Open)
        {
            if (DateTimeOffset.UtcNow - _openedAt >= _openDuration)
            {
                _state = CircuitState.HalfOpen;
                // Allow one probe
                if (Interlocked.CompareExchange(ref _halfOpenProbeInFlight, 1, 0) != 0)
                    return (false, AgentError.CircuitHalfOpenRejected(_name));
                return (true, null);
            }

            var remaining = _openDuration - (DateTimeOffset.UtcNow - _openedAt);
            return (false, AgentError.CircuitOpen(_name, _consecutiveFailures, remaining));
        }

        if (_state == CircuitState.HalfOpen)
        {
            if (Interlocked.CompareExchange(ref _halfOpenProbeInFlight, 1, 0) != 0)
                return (false, AgentError.CircuitHalfOpenRejected(_name));
            return (true, null);
        }

        return (true, null);
    }

    private void OnSuccess()
    {
        _consecutiveFailures = 0;
        _halfOpenProbeInFlight = 0;
        _state = CircuitState.Closed;
    }

    private void OnFailure()
    {
        Interlocked.Increment(ref _consecutiveFailures);

        if (_consecutiveFailures >= _failureThreshold || _state == CircuitState.HalfOpen)
        {
            _state = CircuitState.Open;
            _openedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>Manually resets the circuit to CLOSED (useful for tests or operator intervention).</summary>
    public void Reset()
    {
        _consecutiveFailures = 0;
        _halfOpenProbeInFlight = 0;
        _state = CircuitState.Closed;
    }
}

public enum CircuitState { Closed, Open, HalfOpen }
