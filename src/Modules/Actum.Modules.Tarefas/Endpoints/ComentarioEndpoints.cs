using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Actum.Modules.Tarefas.Endpoints;

/// <summary>
/// HTTP endpoints of the comments sent to the Asana (<c>/tarefas/{identifier}/participantes</c> and
/// <c>/tarefas/{identifier}/comentarios</c>)
/// </summary>
public static class ComentarioEndpoints
{
    /// <summary>
    /// Request body limit of the comment: the 50 MB of files plus the form overhead
    /// </summary>
    public const long LimiteDoCorpo = 52L * 1024 * 1024;

    private const string TarefaNaoEncontrada = "Tarefa não encontrada.";

    /// <summary>
    /// Map the comment endpoints of a task
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapComentarioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tarefas/{identifier:guid}").WithTags("Comentários");

        group.MapGet("/participantes", ListarParticipantesAsync)
            .WithName("ListarParticipantes")
            .WithSummary("Participantes para menção")
            .WithDescription("Lê do Asana, na hora, quem segue a tarefa (exceto o próprio usuário), com a sugestão de quem mencionar: quem fez a reabertura mais recente ou, sem reabertura, quem comentou por último.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        group.MapPost("/comentarios", EnviarAsync)
            .WithName("EnviarComentario")
            .WithSummary("Enviar comentário ao Asana")
            .WithDescription("Formulário multipart com texto, menções (gid dos participantes) e arquivos (até 10, 25 MB cada, 50 MB no total). Anexa os arquivos à tarefa no Asana e publica o comentário com as menções e a lista de anexos; o comentário fica registrado no Actum como lido. Se parte dos anexos já tiver sido enviada quando algo falhar, a resposta traz anexosEnviados.")
            .Accepts<IFormFileCollection>("multipart/form-data")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LimiteDoCorpo))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyList<ParticipanteResponse>>, ProblemHttpResult>> ListarParticipantesAsync(
        Guid identifier,
        IComentarioService service,
        CancellationToken cancellationToken)
    {
        var participantes = await service.ListarParticipantesAsync(identifier, cancellationToken);
        if (participantes is null)
        {
            return TypedResults.Problem(detail: TarefaNaoEncontrada, statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(participantes);
    }

    private static async Task<Results<Created<ComentarioEnviadoResponse>, ProblemHttpResult>> EnviarAsync(
        Guid identifier,
        IFormCollection formulario,
        IComentarioService service,
        CancellationToken cancellationToken)
    {
        // O formulário é lido inteiro: o vinculador da Minimal API não aceita string[] vindo de form nesta versão.
        string? texto = formulario["texto"];
        var mencoes = formulario["mencoes"].OfType<string>().ToList();

        var paraEnviar = formulario.Files
            .Select(arquivo => new ArquivoParaEnviar(
                Path.GetFileName(arquivo.FileName),
                string.IsNullOrWhiteSpace(arquivo.ContentType) ? "application/octet-stream" : arquivo.ContentType,
                arquivo.Length,
                arquivo.OpenReadStream))
            .ToList();

        var enviado = await service.EnviarAsync(identifier, texto, mencoes, paraEnviar, cancellationToken);
        if (enviado is null)
        {
            return TypedResults.Problem(detail: TarefaNaoEncontrada, statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Created((string?)null, enviado);
    }
}
