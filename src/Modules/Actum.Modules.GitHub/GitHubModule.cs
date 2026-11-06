using Actum.Modules.GitHub.Application;
using Actum.Modules.GitHub.Errors;
using Actum.Shared.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Actum.Modules.GitHub;

/// <summary>
/// GitHub module: access to the GitHub REST API on behalf of the user (no database, no endpoints)
/// </summary>
public sealed class GitHubModule : IModule
{
    /// <inheritdoc />
    public string Name => "GitHub";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Nenhum valor de cabeçalho vai para o log: o Authorization carrega o token.
        services.AddHttpClient<IGitHubClient, GitHubClient>(GitHubClient.Configurar)
            .RedactLoggedHeaders(_ => true);

        // GitHubException lançada por qualquer endpoint (inclusive de outros módulos) vira 400/502.
        services.AddExceptionHandler<GitHubExceptionHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // O módulo não expõe endpoints: é usado pelo módulo Tarefas.
    }
}
