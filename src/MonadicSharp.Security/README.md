# MonadicSharp.Security

> Prompt injection detection, secret masking, and tamper-evident audit trail for AI agents — as typed `Result<T>` values.

[![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)](https://www.nuget.org/packages/MonadicSharp.Security)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com)

---

## Overview

`MonadicSharp.Security` wraps any `IAgent<TInput, TOutput>` with a security layer that:

1. **`PromptGuard`** — detects and blocks prompt injection attempts before they reach an LLM
2. **`SecretMasker`** — prevents API keys, JWTs, passwords, and tokens from leaking into logs/traces
3. **`AuditTrail`** — immutable, tamper-evident log of all agent invocations and security events
4. **`SecureAgentWrapper`** — transparent decorator that applies all three to any agent

All violations are typed `SecurityError` values in a `Result<T>` — no unhandled exceptions.

---

## Installation

```bash
dotnet add package MonadicSharp.Security
```

---

## PromptGuard

Detects injection attempts in user input before they reach the pipeline.

```csharp
var guard = PromptGuard.Default;
var result = guard.Validate(userInput);

result.Match(
    onSuccess: safeInput => pipeline.RunAsync(safeInput, context),
    onFailure: error     => Task.FromResult(Result<Output>.Failure(error)));
```

### Detection Rules (Default)

| Rule | Detects |
|------|---------|
| `RoleOverride` | "you are now", "act as", "ignore previous instructions" |
| `SystemPromptLeak` | "show me your system prompt", "repeat your instructions" |
| `DelimiterInjection` | `</system>`, `[INST]`, `<\|im_start\|>`, `###System` |
| `Jailbreak` | "DAN mode", "jailbreak", "bypass safety filter" |
| `CodeInjection` *(Strict only)* | `import os`, `subprocess.run`, `eval(` |

### Modes

```csharp
var guard = PromptGuard.Default; // Standard rules, max 32k chars
var guard = PromptGuard.Strict;  // All rules + CodeInjection, max 8k chars
var guard = new PromptGuard(new PromptGuardOptions
{
    DetectRoleOverride = true,
    MaxInputLength = 4_000,
    RejectBinaryContent = true
});
```

### Sanitize instead of block

```csharp
// Returns cleaned input rather than a failure
var safe = guard.Sanitize(userInput);
```

---

## SecretMasker

Prevents credentials from leaking into logs, traces, and LLM responses.

```csharp
var masker = new SecretMasker();
masker.Register(Environment.GetEnvironmentVariable("OPENAI_KEY")!);

var safe = masker.Mask(llmResponse); // "sk-abc...xyz" → "[MASKED]"
```

### Built-in Patterns

| Pattern | Example |
|---------|---------|
| AWS Access Key | `AKIAIOSFODNN7EXAMPLE` |
| AWS Secret Key | 40-char base64 string |
| JWT Token | `eyJ...` |
| Bearer Token | `Bearer abc123...` |
| API Key | `api_key=secret123` |
| Connection String | `Password=mypass123` |
| GitHub PAT | `ghp_...` |
| OpenAI Key | `sk-...` |
| Slack Token | `xoxb-...` |
| RSA Private Key | `-----BEGIN PRIVATE KEY-----` |

```csharp
// Add custom patterns
masker.AddPattern("MyToken", @"myapp_[A-Za-z0-9]{32}");

// Check without masking
bool hasSecret = masker.ContainsSecret(logLine);

// Mask a headers dictionary
var safeHeaders = masker.MaskDictionary(request.Headers);
```

---

## AuditTrail

Immutable, append-only log of security events. Thread-safe.

```csharp
var audit = new AuditTrail();

audit.Record(AuditEvent.AgentInvoked("SummaryAgent", sessionId));
audit.Record(AuditEvent.InjectionBlocked("RoleOverride", sessionId));
audit.Record(AuditEvent.SecretDetected("JwtToken", "SummaryAgent", sessionId));

// Query
var events = audit.GetAll();
var blocked = audit.GetByType(AuditEventType.InjectionBlocked);
```

---

## SecureAgentWrapper

Transparent decorator that applies PromptGuard + SecretMasker + AuditTrail to any agent.

```csharp
IAgent<string, string> secureAgent = new SecureAgentWrapper<string, string>(
    inner: summaryAgent,
    guard: PromptGuard.Default,
    masker: new SecretMasker().Register(apiKey),
    audit: auditTrail);

// Usage is identical to the unwrapped agent
var result = await secureAgent.ExecuteAsync(userInput, context);
```

---

## Error Codes

| Code | Meaning |
|------|---------|
| `SECURITY_PROMPT_INJECTION` | Injection pattern detected in input |
| `SECURITY_INPUT_TOO_LONG` | Input exceeds MaxInputLength |
| `SECURITY_BINARY_INPUT` | Input contains non-printable characters |
| `SECURITY_SECRET_IN_OUTPUT` | Agent output contains a detectable secret |

---

## DI Registration

```csharp
services.AddMonadicSharpSecurity();
```

---

## License

MIT — part of [MonadicSharp.Framework](../../README.md).
