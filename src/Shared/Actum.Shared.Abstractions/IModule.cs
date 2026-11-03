using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Actum.Shared.Abstractions;

/// <summary>
/// Contract that every business module implements to be composed by the API host
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface IModule
{
    /// <summary>
    /// Module name, used for identification in logs and diagnostics
    /// </summary>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    string Name { get; }

    /// <summary>
    /// Register the module services (DbContext, repositories, application services) in the host container
    /// </summary>
    /// <param name="services">Host service collection</param>
    /// <param name="configuration">Host configuration</param>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Map the module HTTP endpoints in the host
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
