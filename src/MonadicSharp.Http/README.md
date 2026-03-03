# MonadicSharp.Http

> Result-aware HTTP client for .NET 8 — HTTP errors, timeouts, and deserialization failures are typed `Result<T>` values, never unhandled exceptions.

[![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Http)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)

---

## Overview

`MonadicSharp.Http` replaces the fragile `try/catch` pattern around `HttpClient` with Railway-Oriented error handling:

- **`IHttpResultClient`** — typed interface for GET, POST, PUT, PATCH, DELETE
- **`HttpResultClient`** — default implementation backed by `HttpClient`
- **`RetryPolicy`** — typed retry with exponential backoff, composable with `CircuitBreaker`
- **`HttpResultResponse<T>`** — wraps result + status code + response headers
- **`HttpError`** — typed error factory for every HTTP failure scenario

---

## Installation

```bash
dotnet add package MonadicSharp.Http
```

---

## Basic Usage

```csharp
// GET
Result<WeatherForecast> forecast =
    await httpClient.GetAsync<WeatherForecast>("/api/weather");

// POST
Result<OrderResponse> order =
    await httpClient.PostAsync<OrderRequest, OrderResponse>("/api/orders", orderRequest);

// Railway chain
var result = await httpClient
    .GetAsync<UserProfile>($"/api/users/{userId}")
    .BindAsync(profile => enrichmentAgent.ExecuteAsync(profile, context));
```

---

## HttpResultResponse — Access Status & Headers

```csharp
HttpResultResponse<Page> response =
    await httpClient.SendAsync<Page>(new HttpRequestMessage(HttpMethod.Get, "/api/page"));

Console.WriteLine(response.StatusCode);  // HttpStatusCode.OK
Console.WriteLine(response.Result.IsSuccess);
if (response.Headers.TryGetValue("X-Rate-Limit-Remaining", out var remaining))
    Console.WriteLine(remaining);
```

---

## RetryPolicy

```csharp
var retry = new RetryPolicy(
    maxAttempts: 3,
    initialDelay: TimeSpan.FromMilliseconds(200),
    backoffMultiplier: 2.0,
    retryOn: error => error.Code is "HTTP_429" or "HTTP_503");

var result = await retry.ExecuteAsync(
    ct => httpClient.GetAsync<Data>("/api/data", ct));

// On exhaustion: HttpError.RetryExhausted (contains lastError + attempt count)
```

### Integration with CircuitBreaker

```csharp
var breaker = new CircuitBreaker("ExternalApi", failureThreshold: 5);

var result = await retry.ExecuteAsync(
    ct => breaker.ExecuteAsync(
        innerCt => httpClient.GetAsync<Data>("/api/data", innerCt), ct));
```

---

## Error Codes

| Code | HTTP Trigger |
|------|-------------|
| `HTTP_NETWORK_FAILURE` | Network unreachable / DNS failure |
| `HTTP_TIMEOUT` | `HttpClient.Timeout` exceeded |
| `HTTP_CANCELLED` | CancellationToken triggered |
| `HTTP_BAD_REQUEST` | 400 |
| `HTTP_UNAUTHORIZED` | 401 |
| `HTTP_FORBIDDEN` | 403 |
| `HTTP_NOT_FOUND` | 404 |
| `HTTP_CONFLICT` | 409 |
| `HTTP_UNPROCESSABLE_ENTITY` | 422 |
| `HTTP_RATE_LIMITED` | 429 |
| `HTTP_SERVER_ERROR` | 5xx |
| `HTTP_DESERIALIZATION_FAILED` | JSON parse error on success response |
| `HTTP_RETRY_EXHAUSTED` | All retry attempts consumed |

---

## DI Registration

```csharp
// Registers IHttpResultClient + named HttpClient
services.AddMonadicSharpHttp("MyClient", client =>
{
    client.BaseAddress = new Uri("https://api.example.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

---

## License

MIT — part of [MonadicSharp.Framework](../../README.md).
