using System.Diagnostics.Metrics;
using FluentAssertions;
using MonadicSharp.Agents;
using MonadicSharp.Agents.Core;
using MonadicSharp.Telemetry.Core;
using MonadicSharp.Telemetry.Middleware;
using Xunit;

namespace MonadicSharp.Telemetry.Tests;

// ── Stub agents ───────────────────────────────────────────────────────────────

file sealed class SuccessAgent : IAgent<string, string>
{
    public string Name => "SuccessAgent";
    public AgentCapability RequiredCapabilities => AgentCapability.None;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Success(input.ToUpperInvariant()));
}

file sealed class FailureAgent : IAgent<string, string>
{
    public string Name => "FailureAgent";
    public AgentCapability RequiredCapabilities => AgentCapability.None;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Failure(Error.Create("Intentional failure", "TEST_FAIL")));
}

file sealed class ThrowingAgent : IAgent<string, string>
{
    public string Name => "ThrowingAgent";
    public AgentCapability RequiredCapabilities => AgentCapability.None;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
        => throw new InvalidOperationException("Unexpected boom");
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class TelemetryAgentWrapperTests : IDisposable
{
    private readonly AgentMeter _meter;
    private readonly MeterListener _listener;
    private readonly string _meterName;
    private readonly Dictionary<string, List<double>> _histograms = new();
    private readonly Dictionary<string, List<long>> _counters = new();
    private readonly Dictionary<string, List<KeyValuePair<string, object?>>> _lastTags = new();

    private static readonly AgentContext Context = AgentContext.Create(AgentCapability.None, sessionId: "test-session");

    public TelemetryAgentWrapperTests()
    {
        _meterName = $"WrapperTest.{Guid.NewGuid():N}";
        _meter = new AgentMeter(_meterName);

        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == _meterName)
                listener.EnableMeasurementEvents(instrument);
        };

        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            _histograms.GetOrAdd2(instrument.Name).Add(measurement);
            _lastTags[instrument.Name] = tags.ToArray().ToList();
        });

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            _counters.GetOrAdd2(instrument.Name).Add(measurement);
            _lastTags[instrument.Name] = tags.ToArray().ToList();
        });

        _listener.Start();
    }

    // ── Name and capabilities passthrough ─────────────────────────────────────

    [Fact]
    public void Wrapper_ExposesInnerAgentName()
    {
        var wrapper = new TelemetryAgentWrapper<string, string>(new SuccessAgent(), _meter);
        wrapper.Name.Should().Be("SuccessAgent");
    }

    [Fact]
    public void Wrapper_ExposesInnerAgentCapabilities()
    {
        var wrapper = new TelemetryAgentWrapper<string, string>(new SuccessAgent(), _meter);
        wrapper.RequiredCapabilities.Should().Be(AgentCapability.None);
    }

    // ── Metric recording on success ───────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_OnSuccess_RecordsSuccessCounter()
    {
        var wrapper = new TelemetryAgentWrapper<string, string>(new SuccessAgent(), _meter);

        var result = await wrapper.ExecuteAsync("hello", Context);

        result.IsSuccess.Should().BeTrue();
        _counters[TelemetryConstants.AgentExecutionsTotal].Should().ContainSingle().Which.Should().Be(1);
        TagValue(TelemetryConstants.AgentExecutionsTotal, TelemetryConstants.AttrAgentStatus)
            .Should().Be(TelemetryConstants.StatusSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_RecordsDurationHistogram()
    {
        var wrapper = new TelemetryAgentWrapper<string, string>(new SuccessAgent(), _meter);

        await wrapper.ExecuteAsync("hello", Context);

        _histograms[TelemetryConstants.AgentExecutionDuration].Should().ContainSingle()
            .Which.Should().BeGreaterThanOrEqualTo(0);
    }

    // ── Metric recording on failure ───────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_OnFailure_RecordsFailureCounter()
    {
        var wrapper = new TelemetryAgentWrapper<string, string>(new FailureAgent(), _meter);

        var result = await wrapper.ExecuteAsync("hello", Context);

        result.IsFailure.Should().BeTrue();
        TagValue(TelemetryConstants.AgentExecutionsTotal, TelemetryConstants.AttrAgentStatus)
            .Should().Be(TelemetryConstants.StatusFailure);
    }

    [Fact]
    public async Task ExecuteAsync_OnFailure_StillRecordsDuration()
    {
        var wrapper = new TelemetryAgentWrapper<string, string>(new FailureAgent(), _meter);

        await wrapper.ExecuteAsync("hello", Context);

        _histograms[TelemetryConstants.AgentExecutionDuration].Should().ContainSingle();
    }

    // ── Exception passthrough ─────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_OnException_RethrowsAndRecordsFailure()
    {
        var wrapper = new TelemetryAgentWrapper<string, string>(new ThrowingAgent(), _meter);

        var act = async () => await wrapper.ExecuteAsync("hello", Context);

        await act.Should().ThrowAsync<InvalidOperationException>();

        // Metrics should still be recorded even when the inner agent throws
        TagValue(TelemetryConstants.AgentExecutionsTotal, TelemetryConstants.AttrAgentStatus)
            .Should().Be(TelemetryConstants.StatusFailure);
    }

    // ── Agent name tag ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_TagsAgentName()
    {
        var wrapper = new TelemetryAgentWrapper<string, string>(new SuccessAgent(), _meter);
        await wrapper.ExecuteAsync("hello", Context);

        TagValue(TelemetryConstants.AgentExecutionsTotal, TelemetryConstants.AttrAgentName)
            .Should().Be("SuccessAgent");
    }

    // ── Result passthrough ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ReturnsOriginalSuccessValue()
    {
        var wrapper = new TelemetryAgentWrapper<string, string>(new SuccessAgent(), _meter);

        var result = await wrapper.ExecuteAsync("world", Context);

        result.Value.Should().Be("WORLD");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOriginalFailureError()
    {
        var wrapper = new TelemetryAgentWrapper<string, string>(new FailureAgent(), _meter);

        var result = await wrapper.ExecuteAsync("x", Context);

        result.Error.Code.Should().Be("TEST_FAIL");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string? TagValue(string instrumentName, string tagKey)
        => _lastTags.TryGetValue(instrumentName, out var tags)
            ? tags.FirstOrDefault(t => t.Key == tagKey).Value as string
            : null;

    public void Dispose()
    {
        _listener.Dispose();
        _meter.Dispose();
    }
}

file static class DictExtensions2
{
    public static List<T> GetOrAdd2<T>(this Dictionary<string, List<T>> dict, string key)
    {
        if (!dict.TryGetValue(key, out var list))
            dict[key] = list = [];
        return list;
    }
}
