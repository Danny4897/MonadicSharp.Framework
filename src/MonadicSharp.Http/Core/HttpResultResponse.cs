using System.Net;

namespace MonadicSharp.Http.Core;

/// <summary>
/// Combines a typed <see cref="Result{T}"/> with the raw HTTP metadata
/// (status code, headers) so callers can inspect both the deserialized body
/// and the response details in a single Railway-compatible value.
/// </summary>
public sealed class HttpResultResponse<T>
{
    public Result<T> Result { get; }
    public HttpStatusCode StatusCode { get; }
    public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; }

    public bool IsSuccess => Result.IsSuccess;
    public bool IsFailure => Result.IsFailure;
    public T Value => Result.Value;
    public Error Error => Result.Error;

    public HttpResultResponse(
        Result<T> result,
        HttpStatusCode statusCode,
        IReadOnlyDictionary<string, IEnumerable<string>>? headers = null)
    {
        Result = result;
        StatusCode = statusCode;
        Headers = headers ?? new Dictionary<string, IEnumerable<string>>();
    }

    public static implicit operator Result<T>(HttpResultResponse<T> r) => r.Result;
}
