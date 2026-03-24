# MonadicSharp.Framework

> **Enterprise-grade AI agent infrastructure for .NET 8** — failures, security violations, and persistence errors are first-class `Result<T>` values, never unhandled exceptions.
>
> Topics:
csharp dotnet framework clean-architecture dotnet-template nuget dotnet8

[![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/profiles/Danny4897)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

---

## What is MonadicSharp.Framework?

MonadicSharp.Framework is a **monorepo of NuGet packages** that extend the [MonadicSharp](https://github.com/Danny4897/MonadicSharp) Railway-Oriented Programming library with production-ready AI agent infrastructure.

Every module follows the same principle: **typed errors as values, never exceptions**.

```csharp
// Instead of try/catch everywhere:
var result = await AgentPipeline
    .Start("ProcessDocument", extractorAgent)
    .Then(classifierAgent)
    .Then(summaryAgent)
    .RunAsync(input, context);

result.Match(
    onSuccess: output => Console.WriteLine(output),
    onFailure: error  => logger.LogError(error.Code, error.Message));
```

---

## Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| [MonadicSharp.Agents](src/MonadicSharp.Agents/) | Typed agent pipelines, orchestration, circuit breaker | [![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Agents) |
| [MonadicSharp.Security](src/MonadicSharp.Security/) | Prompt injection guard, secret masking, audit trail | [![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Security) |
| [MonadicSharp.Telemetry](src/MonadicSharp.Telemetry/) | OpenTelemetry tracing and metrics for agents | [![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Telemetry) |
| [MonadicSharp.Caching](src/MonadicSharp.Caching/) | Result-aware memory and distributed cache | [![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Caching) |
| [MonadicSharp.Http](src/MonadicSharp.Http/) | Result-aware HTTP client with typed retry | [![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Http) |
| [MonadicSharp.Persistence](src/MonadicSharp.Persistence/) | Result-aware repository and Unit of Work (EF Core 8) | [![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Persistence) |

---

## Architecture

```
MonadicSharp (base library — Result<T>, Error, Unit)
    │
    ├── MonadicSharp.Agents       ← Core orchestration layer
    │       ├── MonadicSharp.Security     ← Wraps agents with security checks
    │       ├── MonadicSharp.Telemetry    ← Wraps agents with OTel spans
    │       └── MonadicSharp.Caching      ← Wraps agents with transparent caching
    │
    ├── MonadicSharp.Http         ← Result-aware HTTP client (used by agents)
    └── MonadicSharp.Persistence  ← Result-aware EF Core repositories
```

All packages depend on **MonadicSharp 1.4.0** and **MonadicSharp.Agents**. They compose via the decorator pattern — you can stack Security + Telemetry + Caching on any `IAgent<TIn, TOut>`.

---

## Quick Start

Install the packages you need:

```bash
dotnet add package MonadicSharp.Agents
dotnet add package MonadicSharp.Security      # optional
dotnet add package MonadicSharp.Telemetry     # optional
dotnet add package MonadicSharp.Caching       # optional
dotnet add package MonadicSharp.Http          # optional
dotnet add package MonadicSharp.Persistence   # optional
```

Register in DI:

```csharp
services
    .AddMonadicSharpAgents()
    .AddMonadicSharpSecurity()
    .AddMonadicSharpTelemetry("MyApp")
    .AddMonadicSharpMemoryCache()
    .AddMonadicSharpHttp()
    .AddMonadicSharpPersistence<AppDbContext>();
```

---

## Repository Structure

```
MonadicSharp.Framework/
├── src/
│   ├── MonadicSharp.Agents/
│   ├── MonadicSharp.Security/
│   ├── MonadicSharp.Telemetry/
│   ├── MonadicSharp.Caching/
│   ├── MonadicSharp.Http/
│   └── MonadicSharp.Persistence/
├── tests/
│   ├── MonadicSharp.Agents.Tests/
│   ├── MonadicSharp.Security.Tests/
│   ├── MonadicSharp.Telemetry.Tests/
│   ├── MonadicSharp.Caching.Tests/
│   ├── MonadicSharp.Http.Tests/
│   └── MonadicSharp.Persistence.Tests/
├── Directory.Build.props
├── global.json              ← pins SDK to 8.0.418
└── MonadicSharp.Framework.sln
```

---

## Contributing

This project is maintained by [Danny4897](https://github.com/Danny4897). Issues and PRs are welcome.

## License

MIT — see [LICENSE](LICENSE).
