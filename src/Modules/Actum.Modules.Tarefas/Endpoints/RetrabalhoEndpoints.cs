using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Actum.Modules.Tarefas.Endpoints;

/// <summary>
/// HTTP endpoints of the rework dashboard (<c>/retrabalhos</c>)
/// </summary>
public static class RetrabalhoEndpoints
{
    /// <summary>
    /// Map the <c>/retrabalhos</c> endpoints
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapRetrabalhoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/retrabalhos").WithTags("Retrabalho");

        group.MapGet("/indicadores", ObterIndicadoresAsync)
            .WithName("ObterIndicadoresDeRetrabalho")
            .WithSummary("Indicadores de retrabalho")
            .WithDescription("Por empresa e no total, no período (pela data da reabertura, até 24 meses): retrabalhos, tarefas com retrabalho, tarefas concluídas, taxa de retrabalho, evolução mensal e as 10 tarefas com mais retrabalhos. Retrabalho é cada 'concluiu' seguido de 'marcou como não concluída' no histórico do Asana.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/reprocessamento", ReprocessarAsync)
            .WithName("ReprocessarHistoricoDeRetrabalho")
            .WithSummary("Reprocessar o histórico do Asana")
            .WithDescription("Lê do Asana o histórico de todas as tarefas e recalcula conclusões e retrabalhos (inclusive os anteriores ao Actum). Uma tarefa que falha mantém os retrabalhos que tinha e aparece em falhas. Não roda junto com a sincronização (409). Só leitura no Asana.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return endpoints;
    }

    private static async Task<Results<Ok<IndicadoresResponse>, ValidationProblem, ProblemHttpResult>> ObterIndicadoresAsync(
        [AsParameters] IndicadoresFiltro filtro,
        IRetrabalhoService service,
        CancellationToken cancellationToken)
    {
        if (ValidacaoDeConsulta.Invalida(filtro) is { } problema)
        {
            return problema;
        }

        var indicadores = await service.ObterIndicadoresAsync(filtro.De!.Value, filtro.Ate!.Value, filtro.Empresa, cancellationToken);
        if (indicadores is null)
        {
            return TypedResults.Problem(detail: "Empresa não encontrada.", statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(indicadores);
    }

    private static async Task<Results<Ok<ReprocessamentoResponse>, ProblemHttpResult>> ReprocessarAsync(
        IRetrabalhoService service,
        CancellationToken cancellationToken)
    {
        var resultado = await service.ReprocessarAsync(cancellationToken);
        if (resultado.EmAndamento)
        {
            return TypedResults.Problem(
                detail: "Uma sincronização ou reprocessamento já está em andamento. Aguarde a conclusão.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.Ok(resultado.Resumo);
    }
}
