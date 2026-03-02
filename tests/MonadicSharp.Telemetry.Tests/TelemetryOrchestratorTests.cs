using System.Diagnostics.Metrics;
using FluentAssertions;
using MonadicSharp.Agents;
using MonadicSharp.Agents.Core;
using MonadicSharp.Agents.Orchestration;
using MonadicSharp.Telemetry.Core;
using MonadicSharp.Telemetry.Orchestration;
using Xunit;

namespace MonadicSharp.Telemetry.Tests;

// ── Stub agents ───────────────────────────────────────────────────────────────

file sealed class EchoAgent : IAgent<string, string>
{
    public string Name => "EchoAgent";
    public AgentCapability RequiredCapabilities => AgentCapability.None;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Success(input));
}

file sealed class BrokenAgent : IAgent<string, string>
{
    public string Name => "BrokenAgent";
    public AgentCapability RequiredCapabilities => AgentCapability.None;
    public Task<Result<string>> ExecuteAsync(string input, AgentContext ctx, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Failure(Error.Create("Agent is broken", "BROKEN")));
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class TelemetryOrchestratorTests : IDisposable
{
    private readonly AgentMeter _meter;
    private readonly MeterListener _listener;
    private readonly string _meterName;
    private readonly Dictionary<string, List<long>> _counters = new();
    private readonly Dictionary<string, List<double>> _histograms = new();
    private readonly Dictionary<string, List<KeyValuePair<string, object?>>> _lastTags = new();

    private static readonly AgentContext Context = AgentContext.Trusted();

    public TelemetryOrchestratorTests()
    {
        _meterName = $"OrchestratorTest.{Guid.NewGuid():N}";
        _meter = new AgentMeter(_meterName);

        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == _meterName)
                listener.EnableMeasurementEvents(instrument);
        };

        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            _histograms.GetOrAdd3(instrument.Name).Add(measurement);
            _lastTags[instrument.Name] = tags.ToArray().ToList();
        });

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            _counters.GetOrAdd3(instrument.Name).Add(measurement);
            _lastTags[instrument.Name] = tags.ToArray().ToList();
        });

        _listener.Start();
    }

    // ── Registration ──────────────────────────────────────────────────────────

    [Fact]
    public void Register_MakesAgentAvailableForDispatch()
    {
        var orchestrator = BuildOrchestrator();
        orchestrator.Register(new EchoAgent());

        orchestrator.RegisteredAgents.Should().Contain("EchoAgent");
    }

    [Fact]
    public async Task Dispatch_ToRegisteredAgent_ReturnsSuccess()
    {
        var orchestrator = BuildOrchestrator();
        orchestrator.Register(new EchoAgent());

        var result = await orchestrator.DispatchAsync<string, string>("EchoAgent", "ping", Context);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ping");
    }

    // ── Telemetry on dispatch ─────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_OnSuccess_RecordsSuccessMetric()
    {
        var orchestrator = BuildOrchestrator();
        orchestrator.Register(new EchoAgent());

        await orchestrator.DispatchAsync<string, string>("EchoAgent", "hello", Context);

        _counters[TelemetryConstants.AgentExecutionsTotal].Should().ContainSingle().Which.Should().Be(1);
        TagValue(TelemetryConstants.AgentExecutionsTotal, TelemetryConstants.AttrAgentStatus)
            .Should().Be(TelemetryConstants.StatusSuccess);
    }

    [Fact]
    public async Task DispatchAsync_OnFailure_RecordsFailureMetric()
    {
        var orchestrator = BuildOrchestrator();
        orchestrator.Register(new BrokenAgent());

        await orchestrator.DispatchAsync<string, string>("BrokenAgent", "hello", Context);

        TagValue(TelemetryConstants.AgentExecutionsTotal, TelemetryConstants.AttrAgentStatus)
            .Should().Be(TelemetryConstants.StatusFailure);
    }

    [Fact]
    public async Task DispatchAsync_RecordsDurationHistogram()
    {
        var orchestrator = BuildOrchestrator();
        orchestrator.Register(new EchoAgent());

        await orchestrator.DispatchAsync<string, string>("EchoAgent", "hello", Context);

        _histograms[TelemetryConstants.AgentExecutionDuration].Should().ContainSingle()
            .Which.Should().BeGreaterThanOrEqualTo(0);
    }

    // ── Unregistered agent ────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_UnregisteredAgent_ReturnsNotFoundFailure()
    {
        var orchestrator = BuildOrchestrator();

        var result = await orchestrator.DispatchAsync<string, string>("NonExistent", "x", Context);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AGENT_NOT_FOUND");
    }

    // ── AuditLog passthrough ──────────────────────────────────────────────────

    [Fact]
    public async Task AuditLog_RecordsDispatches()
    {
        var orchestrator = BuildOrchestrator();
        orchestrator.Register(new EchoAgent());

        await orchestrator.DispatchAsync<string, string>("EchoAgent", "hello", Context);

        orchestrator.AuditLog.Should().ContainSingle()
            .Which.AgentName.Should().Be("EchoAgent");
    }

    // ── GetCircuitState passthrough ───────────────────────────────────────────

    [Fact]
    public void GetCircuitState_RegisteredAgent_ReturnsClosed()
    {
        var orchestrator = BuildOrchestrator();
        orchestrator.Register(new EchoAgent());

        var state = orchestrator.GetCircuitState("EchoAgent");

        state.IsSuccess.Should().BeTrue();
        state.Value.Should().Be(MonadicSharp.Agents.Resilience.CircuitState.Closed);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private TelemetryAgentOrchestrator BuildOrchestrator()
        => new(new AgentOrchestrator(), _meter);

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

file static class DictExtensions3
{
    public static List<T> GetOrAdd3<T>(this Dictionary<string, List<T>> dict, string key)
    {
        if (!dict.TryGetValue(key, out var list))
            dict[key] = list = [];
        return list;
    }
}
