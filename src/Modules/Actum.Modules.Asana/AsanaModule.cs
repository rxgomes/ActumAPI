using Actum.Modules.Asana.Application;
using Actum.Modules.Asana.Endpoints;
using Actum.Modules.Asana.Errors;
using Actum.Shared.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Actum.Modules.Asana;

/// <summary>
/// Asana module: access to the Asana API on behalf of the user (no database)
/// </summary>
public sealed class AsanaModule : IModule
{
    /// <inheritdoc />
    public string Name => "Asana";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Nenhum valor de cabeçalho vai para o log: o Authorization carrega o token.
        services.AddHttpClient<IAsanaClient, AsanaClient>(AsanaClient.Configurar)
            .RedactLoggedHeaders(_ => true);

        // AsanaException lançada por qualquer endpoint (inclusive de outros módulos) vira 400/502.
        services.AddExceptionHandler<AsanaExceptionHandler>();

        // Validação dos DTOs desta compilação (gerada por compilação).
        services.AddValidation();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAsanaEndpoints();
    }
}
