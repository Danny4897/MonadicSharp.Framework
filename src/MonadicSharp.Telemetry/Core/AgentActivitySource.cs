using System.Diagnostics;
using MonadicSharp.Agents.Core;

namespace MonadicSharp.Telemetry.Core;

/// <summary>
/// Provides the shared <see cref="ActivitySource"/> for distributed tracing of agent executions.
///
/// Consumers (OpenTelemetry SDK, Zipkin, Jaeger, etc.) subscribe by listening to
/// <see cref="TelemetryConstants.ActivitySourceName"/>.
///
/// Spans emitted:
/// - "agent.execute"   — one span per agent invocation
/// - "pipeline.run"    — one root span per pipeline execution
/// </summary>
public static class AgentActivitySource
{
    // Lazily created and shared across the application lifetime.
    private static readonly ActivitySource _source =
        new(TelemetryConstants.ActivitySourceName, "1.0.0");

    /// <summary>The underlying <see cref="ActivitySource"/> instance.</summary>
    public static ActivitySource Source => _source;

    // ── Agent span ────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a new span for a single agent execution.
    /// Returns null if no listener is subscribed (zero-overhead when tracing is disabled).
    /// </summary>
    public static Activity? StartAgentActivity(string agentName, AgentContext context)
    {
        var activity = _source.StartActivity(
            "agent.execute",
            ActivityKind.Internal);

        if (activity is null) return null;

        activity.SetTag(TelemetryConstants.AttrAgentName, agentName);
        activity.SetTag(TelemetryConstants.AttrSessionId, context.SessionId);

        return activity;
    }

    /// <summary>
    /// Marks a running agent span as succeeded or failed, then stops it.
    /// </summary>
    public static void CompleteAgentActivity(Activity? activity, bool success, string? errorCode = null)
    {
        if (activity is null) return;

        activity.SetTag(TelemetryConstants.AttrAgentStatus,
            success ? TelemetryConstants.StatusSuccess : TelemetryConstants.StatusFailure);

        if (!success && errorCode is not null)
            activity.SetTag(TelemetryConstants.AttrErrorType, errorCode);

        activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.Stop();
    }

    // ── Pipeline span ─────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a root span for a full pipeline execution.
    /// Returns null if no listener is subscribed.
    /// </summary>
    public static Activity? StartPipelineActivity(string pipelineName, AgentContext context)
    {
        var activity = _source.StartActivity(
            "pipeline.run",
            ActivityKind.Internal);

        if (activity is null) return null;

        activity.SetTag(TelemetryConstants.AttrPipelineName, pipelineName);
        activity.SetTag(TelemetryConstants.AttrSessionId, context.SessionId);

        return activity;
    }

    /// <summary>
    /// Marks a pipeline span as succeeded or failed, then stops it.
    /// </summary>
    public static void CompletePipelineActivity(Activity? activity, bool success, int stepCount, string? errorCode = null)
    {
        if (activity is null) return;

        activity.SetTag(TelemetryConstants.AttrPipelineStatus,
            success ? TelemetryConstants.StatusSuccess : TelemetryConstants.StatusFailure);
        activity.SetTag("pipeline.steps.count", stepCount);

        if (!success && errorCode is not null)
            activity.SetTag(TelemetryConstants.AttrErrorType, errorCode);

        activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.Stop();
    }
}
