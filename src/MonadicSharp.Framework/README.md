# MonadicSharp.Framework

> Meta-package — installs the complete MonadicSharp ecosystem in one command.

```bash
dotnet add package MonadicSharp.Framework
```

## What's included

| Package | Description |
|---|---|
| [`MonadicSharp.Agents`](https://www.nuget.org/packages/MonadicSharp.Agents) | Typed multi-agent orchestration, capability sandboxing, pipelines, circuit breakers |
| [`MonadicSharp.Caching`](https://www.nuget.org/packages/MonadicSharp.Caching) | Result-aware caching (IMemoryCache + IDistributedCache) with agent wrapper |
| [`MonadicSharp.Http`](https://www.nuget.org/packages/MonadicSharp.Http) | Result-aware HTTP client with retry policies and exponential backoff |
| [`MonadicSharp.Persistence`](https://www.nuget.org/packages/MonadicSharp.Persistence) | Result-aware repository + Unit of Work with EF Core 8 |
| [`MonadicSharp.Security`](https://www.nuget.org/packages/MonadicSharp.Security) | Prompt injection detection, audit trails, secret masking |
| [`MonadicSharp.Telemetry`](https://www.nuget.org/packages/MonadicSharp.Telemetry) | OpenTelemetry-compatible metrics and distributed tracing for agents |

## À-la-carte usage

Prefer adding only what you need:

```bash
dotnet add package MonadicSharp.Agents
dotnet add package MonadicSharp.Caching
dotnet add package MonadicSharp.Http
dotnet add package MonadicSharp.Persistence
dotnet add package MonadicSharp.Security
dotnet add package MonadicSharp.Telemetry
```

## License

MIT © 2026 Danny4897
