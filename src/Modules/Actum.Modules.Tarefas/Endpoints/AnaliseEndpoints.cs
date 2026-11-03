using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Actum.Modules.Tarefas.Endpoints;

/// <summary>
/// HTTP endpoints of the AI analyses of a task (<c>/tarefas/{identifier}/analises</c>)
/// </summary>
public static class AnaliseEndpoints
{
    private const string TarefaNaoEncontrada = "Tarefa não encontrada.";

    private const string AnaliseEmAndamento = "Já existe uma análise desta tarefa em andamento. Aguarde a conclusão.";

    /// <summary>
    /// Map the <c>/tarefas/{identifier}/analises</c> endpoints
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapAnaliseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tarefas/{identifier:guid}/analises").WithTags("Análises");

        group.MapPost("/", AnalisarAsync)
            .WithName("AnalisarTarefa")
            .WithSummary("Analisar a demanda com IA")
            .WithDescription("Envia à Claude API o contexto completo da tarefa (empresa, título, descrição, projetos, comentários e retrabalhos) e grava o objetivo, o checklist e as dúvidas como nova versão atual. Itens iguais a itens marcados na versão anterior já entram marcados. Chamada paga; pode levar dezenas de segundos.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapGet("/", ListarAsync)
            .WithName("ListarAnalises")
            .WithSummary("Análises da tarefa")
            .WithDescription("Todas as versões da análise, da mais recente (atual) para a mais antiga, com os itens em ordem. Lista vazia quando a tarefa nunca foi analisada.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/itens/{itemIdentifier:guid}", MarcarItemAsync)
            .WithName("MarcarItemDaAnalise")
            .WithSummary("Marcar item da análise")
            .WithDescription("Marca (item do checklist feito, dúvida resolvida) ou desmarca um item da análise atual. Itens de versões anteriores não podem ser alterados. Nada é enviado ao Asana.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Created<AnaliseResponse>, ProblemHttpResult>> AnalisarAsync(
        Guid identifier,
        IAnaliseService service,
        CancellationToken cancellationToken)
    {
        var resultado = await service.AnalisarAsync(identifier, cancellationToken);

        return resultado.Status switch
        {
            AnaliseResultadoStatus.NaoEncontrada => NaoEncontrada(TarefaNaoEncontrada),
            AnaliseResultadoStatus.EmAndamento => TypedResults.Problem(detail: AnaliseEmAndamento, statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.Created((string?)null, resultado.Analise),
        };
    }

    private static async Task<Results<Ok<IReadOnlyList<AnaliseResponse>>, ProblemHttpResult>> ListarAsync(
        Guid identifier,
        IAnaliseService service,
        CancellationToken cancellationToken)
    {
        var analises = await service.ListarAsync(identifier, cancellationToken);
        if (analises is null)
        {
            return NaoEncontrada(TarefaNaoEncontrada);
        }

        return TypedResults.Ok(analises);
    }

    private static async Task<Results<Ok<AnaliseItemResponse>, ProblemHttpResult>> MarcarItemAsync(
        Guid identifier,
        Guid itemIdentifier,
        MarcarItemRequest request,
        IAnaliseService service,
        CancellationToken cancellationToken)
    {
        var resultado = await service.MarcarItemAsync(identifier, itemIdentifier, request.Concluido!.Value, cancellationToken);
        if (resultado.Status == AnaliseResultadoStatus.NaoEncontrada)
        {
            return NaoEncontrada("Tarefa ou item da análise não encontrado.");
        }

        return TypedResults.Ok(resultado.Item);
    }

    private static ProblemHttpResult NaoEncontrada(string mensagem)
    {
        return TypedResults.Problem(detail: mensagem, statusCode: StatusCodes.Status404NotFound);
    }
}
