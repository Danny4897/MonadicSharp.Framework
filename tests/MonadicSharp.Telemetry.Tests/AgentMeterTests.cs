using System.Diagnostics.Metrics;
using FluentAssertions;
using MonadicSharp.Telemetry.Core;
using Xunit;

namespace MonadicSharp.Telemetry.Tests;

/// <summary>
/// Tests for <see cref="AgentMeter"/> — verifies counters and histograms are
/// recorded with correct values and tags.
/// Uses <see cref="MeterListener"/> (built-in .NET 8) for zero-dependency assertions.
/// </summary>
public class AgentMeterTests : IDisposable
{
    private readonly AgentMeter _meter;
    private readonly MeterListener _listener;
    private readonly string _meterName;

    private readonly Dictionary<string, List<double>> _histograms = new();
    private readonly Dictionary<string, List<long>> _counters = new();
    private readonly Dictionary<string, List<KeyValuePair<string, object?>>> _lastTags = new();

    public AgentMeterTests()
    {
        _meterName = $"Test.{Guid.NewGuid():N}";
        _meter = new AgentMeter(_meterName);

        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == _meterName)
                listener.EnableMeasurementEvents(instrument);
        };

        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            _histograms.GetOrAdd(instrument.Name).Add(measurement);
            _lastTags[instrument.Name] = tags.ToArray().ToList();
        });

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            _counters.GetOrAdd(instrument.Name).Add(measurement);
            _lastTags[instrument.Name] = tags.ToArray().ToList();
        });

        _listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, _) =>
        {
            _histograms.GetOrAdd(instrument.Name).Add(measurement);
            _lastTags[instrument.Name] = tags.ToArray().ToList();
        });

        _listener.Start();
    }

    // ── Agent execution ───────────────────────────────────────────────────────

    [Fact]
    public void RecordAgentExecution_Success_IncrementsCounterAndHistogram()
    {
        _meter.RecordAgentExecution("SummaryAgent", success: true, durationMs: 42.5, sessionId: "sess-01");

        _counters[TelemetryConstants.AgentExecutionsTotal].Should().ContainSingle().Which.Should().Be(1);
        _histograms[TelemetryConstants.AgentExecutionDuration].Should().ContainSingle()
            .Which.Should().BeApproximately(42.5, 0.001);
    }

    [Fact]
    public void RecordAgentExecution_Failure_TagsStatusAsFailure()
    {
        _meter.RecordAgentExecution("SummaryAgent", success: false, durationMs: 10, sessionId: "sess-02");

        TagValue(TelemetryConstants.AgentExecutionsTotal, TelemetryConstants.AttrAgentStatus)
            .Should().Be(TelemetryConstants.StatusFailure);
    }

    [Fact]
    public void RecordAgentExecution_Success_TagsStatusAsSuccess()
    {
        _meter.RecordAgentExecution("SummaryAgent", success: true, durationMs: 5, sessionId: "sess-03");

        TagValue(TelemetryConstants.AgentExecutionsTotal, TelemetryConstants.AttrAgentStatus)
            .Should().Be(TelemetryConstants.StatusSuccess);
    }

    [Fact]
    public void RecordAgentExecution_TagsContainAgentName()
    {
        _meter.RecordAgentExecution("MyAgent", success: true, durationMs: 1, sessionId: "s");

        TagValue(TelemetryConstants.AgentExecutionsTotal, TelemetryConstants.AttrAgentName)
            .Should().Be("MyAgent");
    }

    [Fact]
    public void RecordAgentExecution_DurationHistogram_TagsContainSessionId()
    {
        _meter.RecordAgentExecution("MyAgent", success: true, durationMs: 1, sessionId: "my-session");

        TagValue(TelemetryConstants.AgentExecutionDuration, TelemetryConstants.AttrSessionId)
            .Should().Be("my-session");
    }

    // ── Capability failures ───────────────────────────────────────────────────

    [Fact]
    public void RecordCapabilityFailure_IncrementsCounter()
    {
        _meter.RecordCapabilityFailure("RestrictedAgent");

        _counters[TelemetryConstants.AgentCapabilityFailures].Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public void RecordCapabilityFailure_MultipleCalls_AccumulatesIndividualRecords()
    {
        _meter.RecordCapabilityFailure("RestrictedAgent");
        _meter.RecordCapabilityFailure("RestrictedAgent");
        _meter.RecordCapabilityFailure("RestrictedAgent");

        _counters[TelemetryConstants.AgentCapabilityFailures].Should().HaveCount(3);
    }

    // ── Pipeline execution ────────────────────────────────────────────────────

    [Fact]
    public void RecordPipelineExecution_Success_RecordsAllThreeInstruments()
    {
        _meter.RecordPipelineExecution("DocumentProcessing", success: true, durationMs: 150, stepCount: 3);

        _histograms[TelemetryConstants.PipelineExecutionDuration].Should().ContainSingle()
            .Which.Should().BeApproximately(150, 0.001);
        _counters[TelemetryConstants.PipelineExecutionsTotal].Should().ContainSingle().Which.Should().Be(1);
        _histograms[TelemetryConstants.PipelineStepsCompleted].Should().ContainSingle().Which.Should().Be(3);
    }

    [Fact]
    public void RecordPipelineExecution_Failure_TagsStatusAsFailure()
    {
        _meter.RecordPipelineExecution("MyPipeline", success: false, durationMs: 50, stepCount: 1);

        TagValue(TelemetryConstants.PipelineExecutionsTotal, TelemetryConstants.AttrPipelineStatus)
            .Should().Be(TelemetryConstants.StatusFailure);
    }

    [Fact]
    public void RecordPipelineExecution_TagsPipelineName()
    {
        _meter.RecordPipelineExecution("ExtractorPipeline", success: true, durationMs: 10, stepCount: 2);

        TagValue(TelemetryConstants.PipelineExecutionsTotal, TelemetryConstants.AttrPipelineName)
            .Should().Be("ExtractorPipeline");
    }

    // ── Circuit breaker ───────────────────────────────────────────────────────

    [Fact]
    public void RecordCircuitBreakerTransition_RecordsWithStateTag()
    {
        _meter.RecordCircuitBreakerTransition("SummaryAgent", "open");

        _counters[TelemetryConstants.CircuitBreakerTransitions].Should().ContainSingle().Which.Should().Be(1);
        TagValue(TelemetryConstants.CircuitBreakerTransitions, TelemetryConstants.AttrCircuitState)
            .Should().Be("open");
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

file static class DictExtensions
{
    public static List<T> GetOrAdd<T>(this Dictionary<string, List<T>> dict, string key)
    {
        if (!dict.TryGetValue(key, out var list))
            dict[key] = list = [];
        return list;
    }
}
