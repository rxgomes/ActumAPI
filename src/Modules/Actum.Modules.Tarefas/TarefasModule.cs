using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Endpoints;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions;
using Actum.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Actum.Modules.Tarefas;

/// <summary>
/// Tarefas module: local copy of the Asana tasks of the user, with internal status, due date and rework
/// (schema <c>tarefas</c>)
/// </summary>
public sealed class TarefasModule : IModule
{
    /// <inheritdoc />
    public string Name => "Tarefas";

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TarefasDbContext>(TarefasDbContext.SchemaName);
        services.AddScoped<ISincronizacaoService, SincronizacaoService>();
        services.AddScoped<IEmpresaService, EmpresaService>();
        services.AddScoped<ITarefaService, TarefaService>();
        services.AddScoped<IConclusaoService, ConclusaoService>();
        services.AddScoped<IPainelService, PainelService>();

        // Data de "hoje" do painel; os testes substituem por um relógio fixo.
        services.TryAddSingleton(TimeProvider.System);

        // Uma sincronização por vez no processo; a segunda chamada simultânea recebe 409.
        services.AddSingleton<SincronizacaoEmAndamento>();

        // Validação dos DTOs desta compilação (gerada por compilação).
        services.AddValidation();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSincronizacaoEndpoints();
        endpoints.MapEmpresaEndpoints();
        endpoints.MapPainelEndpoints();
        endpoints.MapTarefaEndpoints();
    }
}
