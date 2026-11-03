using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Actum.Api.HealthChecks;

/// <summary>
/// Registro e mapeamento dos endpoints <c>/health/live</c> e <c>/health/ready</c>.
/// </summary>
internal static class HealthCheckExtensions
{
    private const string LiveTag = "live";

    private const string ReadyTag = "ready";

    public static IServiceCollection AddActumHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag])
            .AddCheck<PostgresHealthCheck>("postgres", tags: [ReadyTag]);

        return services;
    }

    public static IEndpointRouteBuilder MapActumHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // O writer padrão responde só o status agregado (Healthy/Unhealthy), sem detalhes de erro.
        endpoints.MapHealthChecks("/health/live", new() { Predicate = check => check.Tags.Contains(LiveTag) });
        endpoints.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains(ReadyTag) });

        return endpoints;
    }
}
