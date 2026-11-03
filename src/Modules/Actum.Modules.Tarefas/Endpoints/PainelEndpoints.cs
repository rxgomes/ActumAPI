using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Actum.Modules.Tarefas.Endpoints;

/// <summary>
/// HTTP endpoint of the task board (<c>/tarefas/painel</c>)
/// </summary>
public static class PainelEndpoints
{
    /// <summary>
    /// Map the <c>/tarefas/painel</c> endpoint
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapPainelEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tarefas/painel").WithTags("Painel");

        group.MapGet("/", ObterAsync)
            .WithName("ObterPainel")
            .WithSummary("Painel de tarefas")
            .WithDescription("Devolve as tarefas não concluídas e não arquivadas nos grupos retrabalho, entrada, atrasadas, hoje, esta_semana (até domingo), proximas e sem_prazo, nessa ordem e sempre presentes. Filtros opcionais por empresa e texto (título ou descrição, sem diferenciar maiúsculas e acentos).")
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<Results<Ok<PainelResponse>, ValidationProblem>> ObterAsync(
        [AsParameters] PainelFiltro filtro,
        IPainelService service,
        CancellationToken cancellationToken)
    {
        if (ValidacaoDeConsulta.Invalida(filtro) is { } problema)
        {
            return problema;
        }

        return TypedResults.Ok(await service.ObterPainelAsync(filtro, cancellationToken));
    }
}
