using Microsoft.Extensions.DependencyInjection;
using MonadicSharp.Http.Client;
using MonadicSharp.Http.Core;

namespace MonadicSharp.Http.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="HttpResultClient"/> as <see cref="IHttpResultClient"/> using
    /// a named <see cref="System.Net.Http.HttpClient"/> configured by <paramref name="configure"/>.
    /// </summary>
    public static IServiceCollection AddMonadicSharpHttp(
        this IServiceCollection services,
        Action<System.Net.Http.HttpClient>? configure = null)
    {
        var builder = services.AddHttpClient<IHttpResultClient, HttpResultClient>("MonadicSharp.Http");
        if (configure is not null)
            builder.ConfigureHttpClient(configure);
        return services;
    }
}
