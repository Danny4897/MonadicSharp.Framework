using System.Net;

namespace MonadicSharp.Http.Core;

/// <summary>
/// Typed error factory for all HTTP failures.
/// HTTP status codes, network errors, and deserialization problems all produce
/// distinct error codes so callers can handle them precisely in Railway chains.
/// </summary>
public static class HttpError
{
    // ── Network / transport ───────────────────────────────────────────────────

    public static Error NetworkFailure(string url, Exception ex) =>
        Error.FromException(ex, "HTTP_NETWORK_FAILURE")
             .WithMetadata("Url", url);

    public static Error Timeout(string url, TimeSpan elapsed) =>
        Error.Create($"HTTP request to '{url}' timed out after {elapsed.TotalMilliseconds:F0}ms.", "HTTP_TIMEOUT")
             .WithMetadata("Url", url)
             .WithMetadata("ElapsedMs", (long)elapsed.TotalMilliseconds);

    public static Error RequestCancelled(string url) =>
        Error.Create($"HTTP request to '{url}' was cancelled.", "HTTP_CANCELLED")
             .WithMetadata("Url", url);

    // ── Status-code errors ────────────────────────────────────────────────────

    public static Error BadRequest(string url, string? body = null) =>
        StatusError(url, HttpStatusCode.BadRequest, "HTTP_BAD_REQUEST", body);

    public static Error Unauthorized(string url) =>
        StatusError(url, HttpStatusCode.Unauthorized, "HTTP_UNAUTHORIZED");

    public static Error Forbidden(string url) =>
        StatusError(url, HttpStatusCode.Forbidden, "HTTP_FORBIDDEN");

    public static Error NotFound(string url) =>
        StatusError(url, HttpStatusCode.NotFound, "HTTP_NOT_FOUND");

    public static Error Conflict(string url, string? body = null) =>
        StatusError(url, HttpStatusCode.Conflict, "HTTP_CONFLICT", body);

    public static Error UnprocessableEntity(string url, string? body = null) =>
        StatusError(url, HttpStatusCode.UnprocessableEntity, "HTTP_UNPROCESSABLE_ENTITY", body);

    public static Error TooManyRequests(string url, TimeSpan? retryAfter = null)
    {
        var err = StatusError(url, HttpStatusCode.TooManyRequests, "HTTP_RATE_LIMITED");
        return retryAfter.HasValue ? err.WithMetadata("RetryAfterSeconds", (int)retryAfter.Value.TotalSeconds) : err;
    }

    public static Error ServerError(string url, HttpStatusCode statusCode, string? body = null) =>
        StatusError(url, statusCode, "HTTP_SERVER_ERROR", body);

    public static Error UnexpectedStatusCode(string url, HttpStatusCode statusCode, string? body = null) =>
        StatusError(url, statusCode, $"HTTP_{(int)statusCode}", body);

    // ── Deserialization ───────────────────────────────────────────────────────

    public static Error DeserializationFailed(string url, Type targetType, Exception ex) =>
        Error.FromException(ex, "HTTP_DESERIALIZATION_FAILED")
             .WithMetadata("Url", url)
             .WithMetadata("TargetType", targetType.Name);

    // ── Retry exhausted ───────────────────────────────────────────────────────

    public static Error RetryExhausted(string url, int attempts, Error lastError) =>
        Error.Create($"HTTP request to '{url}' failed after {attempts} attempt(s).", "HTTP_RETRY_EXHAUSTED")
             .WithMetadata("Url", url)
             .WithMetadata("Attempts", attempts)
             .WithInnerError(lastError);

    // ── Private helpers ───────────────────────────────────────────────────────

    private static Error StatusError(string url, HttpStatusCode code, string errorCode, string? body = null)
    {
        var err = Error.Create(
            $"HTTP {(int)code} {code} from '{url}'.",
            errorCode)
            .WithMetadata("Url", url)
            .WithMetadata("StatusCode", (int)code);
        return body is not null ? err.WithMetadata("ResponseBody", body) : err;
    }
}
