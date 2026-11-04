using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Shared.Abstractions.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Actum.Modules.Tarefas.Endpoints;

/// <summary>
/// HTTP endpoints of the private notes of a task (<c>/tarefas/{identifier}/anotacoes</c>)
/// </summary>
public static class AnotacaoEndpoints
{
    private const string TarefaNaoEncontrada = "Tarefa não encontrada.";

    private const string AnotacaoNaoEncontrada = "Anotação não encontrada.";

    /// <summary>
    /// Map the <c>/tarefas/{identifier}/anotacoes</c> endpoints
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapAnotacaoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tarefas/{identifier:guid}/anotacoes").WithTags("Anotações");

        group.MapGet("/", ListarAsync)
            .WithName("ListarAnotacoes")
            .WithSummary("Anotações privadas da tarefa")
            .WithDescription("Anotações da tarefa, da mais antiga para a mais recente. Ficam só no Actum, nunca vão para o Asana.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CriarAsync)
            .WithName("CriarAnotacao")
            .WithSummary("Criar anotação privada")
            .WithDescription("Cria uma anotação (até 10.000 caracteres) na tarefa, em qualquer status, inclusive arquivada. Nada é enviado ao Asana.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{anotacaoIdentifier:guid}", AlterarAsync)
            .WithName("AlterarAnotacao")
            .WithSummary("Alterar anotação privada")
            .WithDescription("Altera o texto da anotação e registra quando foi alterada.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{anotacaoIdentifier:guid}", ExcluirAsync)
            .WithName("ExcluirAnotacao")
            .WithSummary("Excluir anotação privada")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyList<AnotacaoResponse>>, ProblemHttpResult>> ListarAsync(
        Guid identifier,
        IAnotacaoService service,
        CancellationToken cancellationToken)
    {
        var anotacoes = await service.ListarAsync(identifier, cancellationToken);
        if (anotacoes is null)
        {
            return NaoEncontrada(TarefaNaoEncontrada);
        }

        return TypedResults.Ok(anotacoes);
    }

    private static async Task<Results<Created<AnotacaoResponse>, ValidationProblem, ProblemHttpResult>> CriarAsync(
        Guid identifier,
        AnotacaoRequest request,
        IAnotacaoService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var anotacao = await service.CriarAsync(identifier, request.Texto!, cancellationToken);
            if (anotacao is null)
            {
                return NaoEncontrada(TarefaNaoEncontrada);
            }

            return TypedResults.Created((string?)null, anotacao);
        }
        catch (DomainException erro)
        {
            return ErroNoTexto(erro);
        }
    }

    private static async Task<Results<Ok<AnotacaoResponse>, ValidationProblem, ProblemHttpResult>> AlterarAsync(
        Guid identifier,
        Guid anotacaoIdentifier,
        AnotacaoRequest request,
        IAnotacaoService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var anotacao = await service.AlterarAsync(identifier, anotacaoIdentifier, request.Texto!, cancellationToken);
            if (anotacao is null)
            {
                return NaoEncontrada(AnotacaoNaoEncontrada);
            }

            return TypedResults.Ok(anotacao);
        }
        catch (DomainException erro)
        {
            return ErroNoTexto(erro);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ExcluirAsync(
        Guid identifier,
        Guid anotacaoIdentifier,
        IAnotacaoService service,
        CancellationToken cancellationToken)
    {
        if (!await service.ExcluirAsync(identifier, anotacaoIdentifier, cancellationToken))
        {
            return NaoEncontrada(AnotacaoNaoEncontrada);
        }

        return TypedResults.NoContent();
    }

    /// <summary>
    /// A text with only spaces passes <c>[Required]</c>; the service rejects it and the message goes to the field
    /// </summary>
    private static ValidationProblem ErroNoTexto(DomainException erro)
    {
        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Texto"] = [erro.Message] });
    }

    private static ProblemHttpResult NaoEncontrada(string mensagem)
    {
        return TypedResults.Problem(detail: mensagem, statusCode: StatusCodes.Status404NotFound);
    }
}
