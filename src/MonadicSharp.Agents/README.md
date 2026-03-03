# MonadicSharp.Agents

> Typed, composable AI agent pipelines for .NET 8 — using Railway-Oriented Programming.

[![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Agents)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)

---

## Overview

`MonadicSharp.Agents` is the **core orchestration layer** of the MonadicSharp.Framework. It provides:

- **`IAgent<TInput, TOutput>`** — the typed contract every agent implements
- **`AgentPipeline`** — sequential pipelines with short-circuit on failure
- **`ParallelAgentPipeline`** — run multiple agents concurrently, collect all results
- **`AgentOrchestrator`** — dispatch by name, manage a registry of agents
- **`CircuitBreaker`** — prevent cascading failures in multi-agent systems
- **`AgentContext`** — capability-based access control per invocation
- **`PipelineResult<T>`** — full execution trace even on partial failure

---

## Installation

```bash
dotnet add package MonadicSharp.Agents
```

---

## Core Concepts

### 1. Define an Agent

```csharp
public class SummaryAgent : IAgent<string, string>
{
    public string Name => "SummaryAgent";
    public AgentCapability RequiredCapabilities => AgentCapability.None;

    public async Task<Result<string>> ExecuteAsync(
        string input,
        AgentContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Result<string>.Failure(AgentError.InvalidInput(Name, "Input cannot be empty"));

        var summary = await CallLlmAsync(input, ct);
        return Result<string>.Success(summary);
    }
}
```

### 2. Build a Pipeline

```csharp
var result = await AgentPipeline
    .Start("DocumentProcessing", new ExtractorAgent())
    .Then(new CleanerAgent())
    .Then(new SummaryAgent())
    .Then(new ClassifierAgent())
    .RunAsync(rawDocument, context);

// result.IsSuccess, result.Steps, result.TotalDuration
Console.WriteLine(result); // Pipeline[DocumentProcessing] OK — 4 steps, 342ms
```

The pipeline **short-circuits on the first failure** — subsequent agents do not run. The `Steps` trace is always populated up to the point of failure.

### 3. AgentContext and Capabilities

```csharp
var context = AgentContext.Create(
    sessionId: Guid.NewGuid().ToString(),
    grantedCapabilities: AgentCapability.ReadDatabase | AgentCapability.CallExternalApi);

// Agents declare what they need:
public AgentCapability RequiredCapabilities => AgentCapability.ReadDatabase;
// The pipeline checks this before calling ExecuteAsync.
```

### 4. Circuit Breaker

```csharp
var breaker = new CircuitBreaker(
    name: "ExternalLlm",
    failureThreshold: 5,
    openDuration: TimeSpan.FromSeconds(30));

var result = await breaker.ExecuteAsync(
    ct => llmAgent.ExecuteAsync(input, context, ct));

// result is Failure with AgentError.CircuitOpen when the circuit is OPEN
```

States: **CLOSED** (normal) → **OPEN** (blocking after threshold) → **HALF-OPEN** (probing) → **CLOSED**.

### 5. Parallel Pipeline

```csharp
var parallelResult = await ParallelAgentPipeline
    .Create<string>("MultiClassifier")
    .Add(sentimentAgent)
    .Add(topicAgent)
    .Add(languageAgent)
    .RunAsync(text, context);

// parallelResult.Results — IReadOnlyList<Result<string>> in order
// parallelResult.AllSucceeded, parallelResult.AnyFailed
```

### 6. AgentOrchestrator

```csharp
var orchestrator = new AgentOrchestrator();
orchestrator.Register("summary", summaryAgent);
orchestrator.Register("classify", classifierAgent);

var result = await orchestrator.RunAsync("summary", input, context);
```

---

## Error Codes

| Code | Meaning |
|------|---------|
| `AGENT_CAPABILITY_DENIED` | Required capability not granted in context |
| `AGENT_PIPELINE_STEP_FAILED` | A pipeline step returned a failure |
| `AGENT_CIRCUIT_OPEN` | CircuitBreaker is in OPEN state |
| `AGENT_CIRCUIT_HALF_OPEN_REJECTED` | Only one probe allowed in HALF-OPEN |
| `AGENT_TIMEOUT` | Operation exceeded the allowed duration |
| `AGENT_CANCELLED` | CancellationToken was triggered |
| `AGENT_UNHANDLED_EXCEPTION` | Unexpected exception caught by the pipeline |
| `AGENT_INVALID_INPUT` | Agent-specific input validation failed |
| `AGENT_NOT_FOUND` | Orchestrator: no agent registered with that name |

---

## DI Registration

```csharp
services.AddMonadicSharpAgents();
```

---

## License

MIT — part of [MonadicSharp.Framework](../../README.md).
