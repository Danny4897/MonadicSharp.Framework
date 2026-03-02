namespace MonadicSharp.Http.Core;

/// <summary>
/// A Result-aware HTTP client. Every method returns a typed <see cref="Result{T}"/> or
/// <see cref="HttpResultResponse{T}"/> — network errors, status codes, and deserialization
/// failures are all first-class typed errors, never unhandled exceptions.
///
/// Implement this interface for custom transports or test doubles.
/// </summary>
public interface IHttpResultClient
{
    /// <summary>GET and deserialize the response body.</summary>
    Task<Result<T>> GetAsync<T>(string url, CancellationToken ct = default);

    /// <summary>POST a serialized body and deserialize the response.</summary>
    Task<Result<TResponse>> PostAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default);

    /// <summary>PUT a serialized body and deserialize the response.</summary>
    Task<Result<TResponse>> PutAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default);

    /// <summary>PATCH a serialized body and deserialize the response.</summary>
    Task<Result<TResponse>> PatchAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct = default);

    /// <summary>DELETE and return the deserialized response body.</summary>
    Task<Result<T>> DeleteAsync<T>(string url, CancellationToken ct = default);

    /// <summary>DELETE with no response body — returns Unit on success.</summary>
    Task<Result<Unit>> DeleteAsync(string url, CancellationToken ct = default);

    /// <summary>Execute an arbitrary <see cref="HttpRequestMessage"/> and deserialize the response.</summary>
    Task<HttpResultResponse<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default);
}
