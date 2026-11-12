using Actum.Modules.Git.Application;
using Actum.Modules.Git.Errors;
using Actum.Shared.Abstractions;
using Actum.Shared.Infrastructure.Processos;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Actum.Modules.Git;

/// <summary>
/// Git module: git command line over the local clones of the user (no database, no endpoints)
/// </summary>
public sealed class GitModule : IModule
{
    /// <inheritdoc />
    public string Name => "Git";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IExecutorDeProcessos, ExecutorDeProcessos>();

        // Uma trava por clone no processo: worktrees do mesmo clone são alterados um por vez.
        services.AddSingleton<TravasDosClones>();
        services.TryAddSingleton<IGitLocal, GitLocal>();

        // GitException lançada por qualquer endpoint (inclusive de outros módulos) vira 400/502.
        services.AddExceptionHandler<GitExceptionHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // O módulo não expõe endpoints: é usado pelo módulo Tarefas.
    }
}
