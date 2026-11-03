using Actum.Modules.Claude.Application;
using Actum.Modules.Claude.Endpoints;
using Actum.Modules.Claude.Errors;
using Actum.Shared.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Actum.Modules.Claude;

/// <summary>
/// Claude module: access to the Claude API (Anthropic) to structure demands (no database)
/// </summary>
public sealed class ClaudeModule : IModule
{
    /// <inheritdoc />
    public string Name => "Claude";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Nenhum valor de cabeçalho vai para o log: o Authorization carrega a chave.
        services.AddHttpClient<IAnalisadorDeDemanda, AnalisadorDeDemanda>(AnalisadorDeDemanda.Configurar)
            .RedactLoggedHeaders(_ => true);

        // ClaudeException lançada por qualquer endpoint (inclusive de outros módulos) vira 400/502.
        services.AddExceptionHandler<ClaudeExceptionHandler>();

        // Validação dos DTOs desta compilação (gerada por compilação).
        services.AddValidation();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapClaudeEndpoints();
    }
}
