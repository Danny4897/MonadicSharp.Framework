using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MonadicSharp.Http.Client;
using Xunit;

namespace MonadicSharp.Http.Tests;

// ── Fake HTTP handler ─────────────────────────────────────────────────────────

file sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(_respond(request));
}

file static class FakeClient
{
    public static HttpResultClient Responding(HttpStatusCode status, object? body = null)
    {
        var handler = new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
                response.Content = JsonContent.Create(body);
            return response;
        });
        return new HttpResultClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });
    }

    public static HttpResultClient Throwing(Exception ex)
    {
        var handler = new FakeHandler(_ => throw ex);
        return new HttpResultClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });
    }
}

file sealed record WeatherDto(string City, double TempC);

// ── Tests ─────────────────────────────────────────────────────────────────────

public class HttpResultClientTests
{
    // ── GET success ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_200_DeserializesBody()
    {
        var client = FakeClient.Responding(HttpStatusCode.OK, new WeatherDto("Rome", 22.5));

        var result = await client.GetAsync<WeatherDto>("/weather");

        result.IsSuccess.Should().BeTrue();
        result.Value.City.Should().Be("Rome");
        result.Value.TempC.Should().Be(22.5);
    }

    // ── HTTP status codes → typed errors ─────────────────────────────────────

    [Fact]
    public async Task Get_404_ReturnsNotFoundError()
    {
        var result = await FakeClient.Responding(HttpStatusCode.NotFound).GetAsync<WeatherDto>("/weather");
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HTTP_NOT_FOUND");
    }

    [Fact]
    public async Task Get_401_ReturnsUnauthorizedError()
    {
        var result = await FakeClient.Responding(HttpStatusCode.Unauthorized).GetAsync<WeatherDto>("/weather");
        result.Error.Code.Should().Be("HTTP_UNAUTHORIZED");
    }

    [Fact]
    public async Task Get_403_ReturnsForbiddenError()
    {
        var result = await FakeClient.Responding(HttpStatusCode.Forbidden).GetAsync<WeatherDto>("/weather");
        result.Error.Code.Should().Be("HTTP_FORBIDDEN");
    }

    [Fact]
    public async Task Get_400_ReturnsBadRequestError()
    {
        var result = await FakeClient.Responding(HttpStatusCode.BadRequest).GetAsync<WeatherDto>("/weather");
        result.Error.Code.Should().Be("HTTP_BAD_REQUEST");
    }

    [Fact]
    public async Task Get_500_ReturnsServerError()
    {
        var result = await FakeClient.Responding(HttpStatusCode.InternalServerError).GetAsync<WeatherDto>("/weather");
        result.Error.Code.Should().Be("HTTP_SERVER_ERROR");
    }

    [Fact]
    public async Task Get_429_ReturnsRateLimitedError()
    {
        var result = await FakeClient.Responding(HttpStatusCode.TooManyRequests).GetAsync<WeatherDto>("/weather");
        result.Error.Code.Should().Be("HTTP_RATE_LIMITED");
    }

    // ── Network failure ───────────────────────────────────────────────────────

    [Fact]
    public async Task Get_NetworkException_ReturnsNetworkFailureError()
    {
        var result = await FakeClient.Throwing(new HttpRequestException("Connection refused"))
            .GetAsync<WeatherDto>("/weather");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HTTP_NETWORK_FAILURE");
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_CancelledToken_ReturnsCancelledError()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await FakeClient.Responding(HttpStatusCode.OK).GetAsync<WeatherDto>("/weather", cts.Token);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HTTP_CANCELLED");
    }

    // ── POST ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_201_DeserializesResponse()
    {
        var client = FakeClient.Responding(HttpStatusCode.Created, new WeatherDto("Milan", 18.0));

        var result = await client.PostAsync<WeatherDto, WeatherDto>("/weather", new WeatherDto("Milan", 18.0));

        result.IsSuccess.Should().BeTrue();
        result.Value.City.Should().Be("Milan");
    }

    // ── DELETE no-body ────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_204_ReturnsUnitSuccess()
    {
        var result = await FakeClient.Responding(HttpStatusCode.NoContent).DeleteAsync("/weather/1");
        result.IsSuccess.Should().BeTrue();
    }

    // ── Deserialization failure ───────────────────────────────────────────────

    [Fact]
    public async Task Get_200_InvalidJson_ReturnsDeserializationError()
    {
        var handler = new FakeHandler(_ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.OK);
            r.Content = new StringContent("not-json", System.Text.Encoding.UTF8, "application/json");
            return r;
        });
        var client = new HttpResultClient(new HttpClient(handler) { BaseAddress = new Uri("https://test.local") });

        var result = await client.GetAsync<WeatherDto>("/weather");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HTTP_DESERIALIZATION_FAILED");
    }
}
