using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Actum.Modules.Tarefas.Endpoints;

/// <summary>
/// HTTP endpoints of the synchronization with the Asana (<c>/sincronizacoes</c>)
/// </summary>
public static class SincronizacaoEndpoints
{
    private const string EmAndamentoMessage = "Já existe uma sincronização em andamento. Aguarde o término e tente novamente.";

    /// <summary>
    /// Map the <c>/sincronizacoes</c> endpoints
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapSincronizacaoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/sincronizacoes").WithTags("Sincronizações");

        group.MapPost("/", SincronizarAsync)
            .WithName("Sincronizar")
            .WithSummary("Sincronizar tarefas com o Asana")
            .WithDescription("Sincroniza todas as empresas com sincronizar=true: cria empresas para workspaces novos, cria e atualiza tarefas, importa comentários, detecta reaberturas e arquiva tarefas que saíram da lista do usuário. Responde com o resumo da execução.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapGet("/ultima", ObterUltimaAsync)
            .WithName("ObterUltimaSincronizacao")
            .WithSummary("Consultar a última sincronização")
            .WithDescription("Devolve data, resultado e totais da sincronização mais recente.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<SincronizacaoResponse>, ProblemHttpResult>> SincronizarAsync(
        ISincronizacaoService service,
        CancellationToken cancellationToken)
    {
        var resultado = await service.SincronizarAsync(cancellationToken);
        if (resultado.Status == SincronizacaoResultadoStatus.EmAndamento)
        {
            return TypedResults.Problem(detail: EmAndamentoMessage, statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.Ok(resultado.Sincronizacao);
    }

    private static async Task<Results<Ok<SincronizacaoResponse>, ProblemHttpResult>> ObterUltimaAsync(
        ISincronizacaoService service,
        CancellationToken cancellationToken)
    {
        var ultima = await service.ObterUltimaAsync(cancellationToken);
        if (ultima is null)
        {
            return TypedResults.Problem(detail: "Nenhuma sincronização foi executada ainda.", statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(ultima);
    }
}
