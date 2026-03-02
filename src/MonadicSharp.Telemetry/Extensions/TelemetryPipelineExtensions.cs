using System.Diagnostics;
using MonadicSharp.Agents.Core;
using MonadicSharp.Agents.Pipeline;
using MonadicSharp.Telemetry.Core;

namespace MonadicSharp.Telemetry.Extensions;

/// <summary>
/// Extension methods that add OpenTelemetry instrumentation to agent pipelines.
///
/// Usage:
/// <code>
/// var result = await AgentPipeline
///     .Start("DocumentProcessing", extractorAgent)
///     .Then(summaryAgent)
///     .RunWithTelemetryAsync(input, context, AgentMeter.Instance);
/// </code>
/// </summary>
public static class TelemetryPipelineExtensions
{
    /// <summary>
    /// Runs the pipeline and records metrics + a distributed trace span.
    ///
    /// Emits:
    /// - A "pipeline.run" Activity span tagged with pipeline name, step count, and status
    /// - <c>pipeline.execution.duration</c> histogram (ms)
    /// - <c>pipeline.executions.total</c> counter
    /// - <c>pipeline.steps.completed</c> histogram
    /// </summary>
    public static async Task<PipelineResult<TOutput>> RunWithTelemetryAsync<TInput, TOutput>(
        this AgentPipeline<TInput, TOutput> pipeline,
        TInput input,
        AgentContext context,
        AgentMeter? meter = null,
        CancellationToken cancellationToken = default)
    {
        var agentMeter = meter ?? AgentMeter.Instance;

        // Start a span before execution so it covers the full duration.
        // We tag the pipeline name after RunAsync because AgentPipeline<T,T>
        // does not expose its name until PipelineResult is returned.
        var activity = AgentActivitySource.Source.StartActivity("pipeline.run", ActivityKind.Internal);
        activity?.SetTag(TelemetryConstants.AttrSessionId, context.SessionId);

        try
        {
            var result = await pipeline.RunAsync(input, context, cancellationToken)
                .ConfigureAwait(false);

            // Now we have the actual name from the result.
            activity?.SetTag(TelemetryConstants.AttrPipelineName, result.PipelineName);

            var durationMs = result.TotalDuration.TotalMilliseconds;
            var stepCount = result.Steps.Count;

            agentMeter.RecordPipelineExecution(result.PipelineName, result.IsSuccess, durationMs, stepCount);

            AgentActivitySource.CompletePipelineActivity(
                activity,
                result.IsSuccess,
                stepCount,
                result.IsFailure ? result.Error.Code : null);

            return result;
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.Stop();
            throw;
        }
    }
}
