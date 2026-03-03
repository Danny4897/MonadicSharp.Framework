# MonadicSharp.Telemetry

> OpenTelemetry tracing and metrics for AI agent pipelines — zero overhead when disabled.

[![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Telemetry)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)

---

## Overview

`MonadicSharp.Telemetry` integrates agent pipelines with the OpenTelemetry ecosystem:

- **Distributed tracing** — one `Activity` span per agent, one root span per pipeline
- **Metrics** — execution count, latency histograms, failure rates via `System.Diagnostics.Metrics`
- **`TelemetryAgentWrapper`** — transparent decorator that instruments any `IAgent<TIn, TOut>`
- **`TelemetryAgentOrchestrator`** — instrumented orchestrator with per-dispatch spans
- **`TelemetryPipelineExtensions`** — `WithTelemetry()` fluent extension for pipelines
- **Zero overhead** when no listener is subscribed (standard .NET `ActivitySource` model)

---

## Installation

```bash
dotnet add package MonadicSharp.Telemetry
```

---

## Setup

### DI Registration

```csharp
services.AddMonadicSharpTelemetry("MyApp");
```

### Hook into your OpenTelemetry pipeline

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(TelemetryConstants.ActivitySourceName) // "MonadicSharp.Agents"
        .AddJaegerExporter()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(TelemetryConstants.MeterName)           // "MonadicSharp.Agents"
        .AddPrometheusExporter());
```

---

## Tracing

### Instrument a single agent

```csharp
IAgent<string, string> tracedAgent = new TelemetryAgentWrapper<string, string>(
    inner: summaryAgent,
    activitySource: AgentActivitySource.Source);
```

### Instrument a pipeline

```csharp
var result = await AgentPipeline
    .Start("DocumentProcessing", extractorAgent)
    .Then(classifierAgent)
    .WithTelemetry()           // adds pipeline root span
    .RunAsync(input, context);
```

### Spans emitted

| Span name | Tags |
|-----------|------|
| `agent.execute` | `agent.name`, `agent.status`, `session.id`, `error.type` |
| `pipeline.run` | `pipeline.name`, `pipeline.status`, `pipeline.steps.count`, `session.id` |

---

## Metrics

```csharp
// Injected via DI
public class MyService(AgentMeter meter)
{
    public async Task RunAsync(...)
    {
        meter.RecordExecution("SummaryAgent", success: true, durationMs: 120);
        meter.RecordPipelineRun("DocumentProcessing", stepCount: 3, success: true);
    }
}
```

### Instruments registered

| Instrument | Type | Description |
|-----------|------|-------------|
| `agent.executions` | Counter | Total agent invocations |
| `agent.failures` | Counter | Total agent failures |
| `agent.duration_ms` | Histogram | Per-agent execution latency |
| `pipeline.runs` | Counter | Total pipeline executions |
| `pipeline.failures` | Counter | Total pipeline failures |
| `pipeline.duration_ms` | Histogram | End-to-end pipeline latency |

---

## Telemetry Constants

```csharp
TelemetryConstants.ActivitySourceName // "MonadicSharp.Agents"
TelemetryConstants.MeterName          // "MonadicSharp.Agents"
TelemetryConstants.AttrAgentName      // "agent.name"
TelemetryConstants.AttrPipelineName   // "pipeline.name"
TelemetryConstants.AttrSessionId      // "session.id"
```

---

## TelemetryAgentOrchestrator

Drop-in replacement for `AgentOrchestrator` with automatic span wrapping:

```csharp
var orchestrator = new TelemetryAgentOrchestrator(AgentActivitySource.Source);
orchestrator.Register("summary", summaryAgent);

// Each RunAsync call emits an "agent.execute" span
var result = await orchestrator.RunAsync("summary", input, context);
```

---

## License

MIT — part of [MonadicSharp.Framework](../../README.md).
