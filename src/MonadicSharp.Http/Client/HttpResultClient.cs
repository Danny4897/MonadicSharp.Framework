using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MonadicSharp.Http.Core;

namespace MonadicSharp.Http.Client;

/// <summary>
/// Default <see cref="IHttpResultClient"/> backed by a standard <see cref="HttpClient"/>.
/// All HTTP errors, network failures, and deserialization problems are mapped to typed
/// <see cref="Result{T}"/> failures — no unhandled exceptions escape.
/// </summary>
public sealed class HttpResultClient : IHttpResultClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;
    private readonly ILogger<HttpResultClient>? _logger;

    public HttpResultClient(
        HttpClient http,
        JsonSerializerOptions? jsonOptions = null,
        ILogger<HttpResultClient>? logger = null)
    {
        _http = http;
        _json = jsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _logger = logger;
    }

    public Task<Result<T>> GetAsync<T>(string url, CancellationToken ct = default) =>
        ExecuteAndDeserialize<T>(HttpMethod.Get, url, body: null, ct);

    public Task<Result<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default) =>
        ExecuteAndDeserialize<TResponse>(HttpMethod.Post, url, body, ct);

    public Task<Result<TResponse>> PutAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default) =>
        ExecuteAndDeserialize<TResponse>(HttpMethod.Put, url, body, ct);

    public Task<Result<TResponse>> PatchAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default) =>
        ExecuteAndDeserialize<TResponse>(HttpMethod.Patch, url, body, ct);

    public Task<Result<T>> DeleteAsync<T>(string url, CancellationToken ct = default) =>
        ExecuteAndDeserialize<T>(HttpMethod.Delete, url, body: null, ct);

    public async Task<Result<Unit>> DeleteAsync(string url, CancellationToken ct = default)
    {
        var result = await ExecuteAndDeserialize<Unit>(HttpMethod.Delete, url, body: null, ct).ConfigureAwait(false);
        // 204 No Content: if deserialization fails but status was success, return Unit
        return result.IsSuccess ? result : Result<Unit>.Success(Unit.Value);
    }

    public async Task<HttpResultResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default)
    {
        HttpResponseMessage? response = null;
        try
        {
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var headers = response.Headers.ToDictionary(
                h => h.Key,
                h => (IEnumerable<string>)h.Value);

            var result = await DeserializeResponse<T>(response, request.RequestUri?.ToString() ?? "", ct).ConfigureAwait(false);
            return new HttpResultResponse<T>(result, response.StatusCode, headers);
        }
        catch (OperationCanceledException)
        {
            var url = request.RequestUri?.ToString() ?? "";
            return new HttpResultResponse<T>(Result<T>.Failure(HttpError.RequestCancelled(url)), 0);
        }
        catch (HttpRequestException ex)
        {
            var url = request.RequestUri?.ToString() ?? "";
            return new HttpResultResponse<T>(Result<T>.Failure(HttpError.NetworkFailure(url, ex)), 0);
        }
        finally
        {
            response?.Dispose();
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<Result<T>> ExecuteAndDeserialize<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        HttpResponseMessage? response = null;
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body is not null)
                request.Content = JsonContent.Create(body, options: _json);

            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            return await DeserializeResponse<T>(response, url, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return Result<T>.Failure(HttpError.Timeout(url, _http.Timeout));
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure(HttpError.RequestCancelled(url));
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network failure calling {Url}", url);
            return Result<T>.Failure(HttpError.NetworkFailure(url, ex));
        }
        finally
        {
            response?.Dispose();
        }
    }

    private async Task<Result<T>> DeserializeResponse<T>(HttpResponseMessage response, string url, CancellationToken ct)
    {
        var statusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger?.LogWarning("HTTP {StatusCode} from {Url}", statusCode, url);

            return Result<T>.Failure(statusCode switch
            {
                400 => HttpError.BadRequest(url, body),
                401 => HttpError.Unauthorized(url),
                403 => HttpError.Forbidden(url),
                404 => HttpError.NotFound(url),
                409 => HttpError.Conflict(url, body),
                422 => HttpError.UnprocessableEntity(url, body),
                429 => HttpError.TooManyRequests(url),
                >= 500 => HttpError.ServerError(url, response.StatusCode, body),
                _ => HttpError.UnexpectedStatusCode(url, response.StatusCode, body)
            });
        }

        // 204 No Content
        if (statusCode == 204 || response.Content.Headers.ContentLength == 0)
        {
            if (typeof(T) == typeof(Unit))
                return Result<T>.Success((T)(object)Unit.Value);
        }

        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(_json, ct).ConfigureAwait(false);
            return value is null
                ? Result<T>.Failure(HttpError.DeserializationFailed(url, typeof(T), new InvalidOperationException("Response body was null.")))
                : Result<T>.Success(value);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Deserialization failed for {Url} → {Type}", url, typeof(T).Name);
            return Result<T>.Failure(HttpError.DeserializationFailed(url, typeof(T), ex));
        }
    }
}
