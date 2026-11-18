using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Actum.Modules.Tarefas.Endpoints;

/// <summary>
/// HTTP endpoints of the files attached to the task in the Asana (<c>/tarefas/{identifier}/anexos</c>)
/// </summary>
public static class AnexoEndpoints
{
    private const string TarefaNaoEncontrada = "Tarefa não encontrada.";

    private const string AnexoNaoEncontrado = "Anexo não encontrado nesta tarefa.";

    /// <summary>
    /// Map the attachment endpoints of a task
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapAnexoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tarefas/{identifier:guid}/anexos").WithTags("Anexos");

        group.MapGet("/", ListarAsync)
            .WithName("ListarAnexos")
            .WithSummary("Anexos da tarefa")
            .WithDescription("Lê do Asana, na hora, os arquivos anexados à tarefa e aos seus comentários, do mais antigo para o mais recente.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapGet("/{anexoGid}/download", BaixarAsync)
            .WithName("BaixarAnexo")
            .WithSummary("Baixar anexo")
            .WithDescription("Pede ao Asana um endereço novo para baixar o anexo (os endereços do Asana expiram em poucos minutos) e redireciona para ele. Para arquivos externos (Google Drive, Dropbox...), redireciona para o endereço de abertura.")
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyList<AnexoResponse>>, ProblemHttpResult>> ListarAsync(
        Guid identifier,
        IAnexoService service,
        CancellationToken cancellationToken)
    {
        var anexos = await service.ListarAsync(identifier, cancellationToken);
        if (anexos is null)
        {
            return TypedResults.Problem(detail: TarefaNaoEncontrada, statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(anexos);
    }

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> BaixarAsync(
        Guid identifier,
        string anexoGid,
        IAnexoService service,
        CancellationToken cancellationToken)
    {
        var endereco = await service.ObterEnderecoDeDownloadAsync(identifier, anexoGid, cancellationToken);
        if (endereco is null)
        {
            return TypedResults.Problem(detail: AnexoNaoEncontrado, statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Redirect(endereco.AbsoluteUri);
    }
}
