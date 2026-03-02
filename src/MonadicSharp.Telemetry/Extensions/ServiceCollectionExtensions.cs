using Microsoft.Extensions.DependencyInjection;
using MonadicSharp.Agents.Orchestration;
using MonadicSharp.Telemetry.Core;
using MonadicSharp.Telemetry.Orchestration;

namespace MonadicSharp.Telemetry.Extensions;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering MonadicSharp telemetry.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AgentMeter"/> as a singleton and wraps any existing
    /// <see cref="AgentOrchestrator"/> registration with <see cref="TelemetryAgentOrchestrator"/>.
    ///
    /// After calling this, resolve <see cref="TelemetryAgentOrchestrator"/> from DI
    /// instead of <see cref="AgentOrchestrator"/> directly.
    /// </summary>
    public static IServiceCollection AddMonadicSharpTelemetry(this IServiceCollection services)
    {
        services.AddSingleton(AgentMeter.Instance);

        services.AddSingleton(sp =>
        {
            var inner = sp.GetRequiredService<AgentOrchestrator>();
            var meter = sp.GetRequiredService<AgentMeter>();
            return new TelemetryAgentOrchestrator(inner, meter);
        });

        return services;
    }
}
