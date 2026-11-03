using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Actum.Api.HealthChecks;

/// <summary>
/// Verifica se o PostgreSQL do Actum está acessível executando <c>select 1</c>.
/// Exceções de conexão são convertidas em <c>Unhealthy</c> pelo próprio serviço de health checks.
/// </summary>
internal sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var comando = dataSource.CreateCommand("select 1");
        await comando.ExecuteScalarAsync(cancellationToken);

        return HealthCheckResult.Healthy();
    }
}
