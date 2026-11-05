using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Shared.Abstractions.Pagination;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Actum.Modules.Tarefas.Endpoints;

/// <summary>
/// HTTP endpoints of the tasks (<c>/tarefas</c>)
/// </summary>
public static class TarefaEndpoints
{
    private const string TarefaNaoEncontrada = "Tarefa não encontrada.";

    private const string BranchRepetida = "A tarefa já tem uma branch com esse nome.";

    /// <summary>
    /// Map the <c>/tarefas</c> endpoints
    /// </summary>
    /// <param name="endpoints">Host endpoint route builder</param>
    /// <returns>The same endpoint route builder</returns>
    public static IEndpointRouteBuilder MapTarefaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tarefas").WithTags("Tarefas");

        group.MapGet("/", ListarAsync)
            .WithName("ListarTarefas")
            .WithSummary("Listar tarefas")
            .WithDescription("Listagem paginada (tamanho padrão 20, máximo 100), das mais recentes para as mais antigas, com filtros opcionais por empresa, status (inclui concluida e arquivada) e texto (título ou descrição, sem diferenciar maiúsculas e acentos).")
            .ProducesValidationProblem();

        group.MapGet("/{identifier:guid}", ObterDetalheAsync)
            .WithName("ObterTarefa")
            .WithSummary("Detalhe da tarefa")
            .WithDescription("Devolve os dados da tarefa, as branches (indicando se já foram enviadas ao Asana), os retrabalhos em ordem, cada um com os comentários do dia da reabertura até a reabertura seguinte, e todos os comentários.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{identifier:guid}/comentarios/marcar-lidos", MarcarComentariosLidosAsync)
            .WithName("MarcarComentariosLidos")
            .WithSummary("Marcar comentários como lidos")
            .WithDescription("Marca como lidos todos os comentários da tarefa.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{identifier:guid}", AlterarAsync)
            .WithName("AlterarTarefa")
            .WithSummary("Alterar prazo interno e prioridade")
            .WithDescription("Altera o prazo interno e/ou a prioridade (1 a 3) de uma tarefa não arquivada. Campos não enviados são mantidos. Em tarefa reaberta, informar o prazo resolve a pendência. Nada é enviado ao Asana.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{identifier:guid}/iniciar", IniciarAsync)
            .WithName("IniciarTarefa")
            .WithSummary("Iniciar tarefa")
            .WithDescription("Inicia uma tarefa da entrada com a branch base, a branch de implementação (diferentes) e o prazo interno. As branches só são enviadas ao Asana na conclusão.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{identifier:guid}/retomar", RetomarAsync)
            .WithName("RetomarTarefa")
            .WithSummary("Retomar tarefa (Em andamento)")
            .WithDescription("Põe em andamento uma tarefa pausada, já iniciada ou em retrabalho. Tarefas bloqueadas precisam ser desbloqueadas antes. Nada é enviado ao Asana.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{identifier:guid}/pausar", PausarAsync)
            .WithName("PausarTarefa")
            .WithSummary("Pausar tarefa")
            .WithDescription("Pausa uma tarefa em andamento. Nada é enviado ao Asana.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{identifier:guid}/bloquear", BloquearAsync)
            .WithName("BloquearTarefa")
            .WithSummary("Bloquear tarefa")
            .WithDescription("Bloqueia uma tarefa em andamento ou em retrabalho (aguardando resposta de alguém).")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{identifier:guid}/desbloquear", DesbloquearAsync)
            .WithName("DesbloquearTarefa")
            .WithSummary("Desbloquear tarefa")
            .WithDescription("Desbloqueia a tarefa, que volta ao status que tinha antes do bloqueio.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{identifier:guid}/branches", AdicionarBranchAsync)
            .WithName("AdicionarBranch")
            .WithSummary("Registrar branch")
            .WithDescription("Registra uma branch (base ou implementacao) em tarefa em andamento, bloqueada ou em retrabalho. Em retrabalho, a branch fica vinculada ao retrabalho aberto. Nomes não se repetem na tarefa.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{identifier:guid}/branches/{branchIdentifier:guid}", RemoverBranchAsync)
            .WithName("RemoverBranch")
            .WithSummary("Excluir branch")
            .WithDescription("Exclui uma branch ainda não enviada ao Asana (para corrigir erro de digitação). Branches enviadas não podem ser excluídas.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{identifier:guid}/conclusao/previa", ObterPreviaAsync)
            .WithName("ObterPreviaDaConclusao")
            .WithSummary("Prévia da conclusão")
            .WithDescription("Informa as tags que serão adicionadas no Asana (branches ainda não enviadas) e se a conclusão é permitida, com o motivo quando não for. Não consulta o Asana.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{identifier:guid}/concluir", ConcluirAsync)
            .WithName("ConcluirTarefa")
            .WithSummary("Concluir tarefa")
            .WithDescription("Adiciona no Asana, como tags com o nome exato, só as branches ainda não enviadas, conclui a tarefa no Asana e no Actum e resolve o retrabalho aberto. Se o Asana falhar no meio, uma nova tentativa continua de onde parou.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return endpoints;
    }

    private static async Task<Results<Ok<PagedResult<CartaoTarefaResponse>>, ValidationProblem>> ListarAsync(
        [AsParameters] ListarTarefasFiltro filtro,
        IPainelService service,
        CancellationToken cancellationToken)
    {
        if (ValidacaoDeConsulta.Invalida(filtro) is { } problema)
        {
            return problema;
        }

        return TypedResults.Ok(await service.ListarAsync(filtro, cancellationToken));
    }

    private static async Task<Results<Ok<TarefaDetalheResponse>, ProblemHttpResult>> ObterDetalheAsync(
        Guid identifier,
        IPainelService service,
        CancellationToken cancellationToken)
    {
        var detalhe = await service.ObterDetalheAsync(identifier, cancellationToken);
        if (detalhe is null)
        {
            return NaoEncontrada();
        }

        return TypedResults.Ok(detalhe);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> MarcarComentariosLidosAsync(
        Guid identifier,
        IPainelService service,
        CancellationToken cancellationToken)
    {
        if (!await service.MarcarComentariosLidosAsync(identifier, cancellationToken))
        {
            return NaoEncontrada();
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<TarefaCicloResponse>, ProblemHttpResult>> AlterarAsync(
        Guid identifier,
        AlterarTarefaRequest request,
        ITarefaService service,
        CancellationToken cancellationToken)
    {
        return ParaResposta(await service.AlterarAsync(identifier, request, cancellationToken));
    }

    private static async Task<Results<Ok<TarefaCicloResponse>, ProblemHttpResult>> IniciarAsync(
        Guid identifier,
        IniciarTarefaRequest request,
        ITarefaService service,
        CancellationToken cancellationToken)
    {
        return ParaResposta(await service.IniciarAsync(identifier, request, cancellationToken));
    }

    private static async Task<Results<Ok<TarefaCicloResponse>, ProblemHttpResult>> RetomarAsync(
        Guid identifier,
        ITarefaService service,
        CancellationToken cancellationToken)
    {
        return ParaResposta(await service.RetomarAsync(identifier, cancellationToken));
    }

    private static async Task<Results<Ok<TarefaCicloResponse>, ProblemHttpResult>> PausarAsync(
        Guid identifier,
        ITarefaService service,
        CancellationToken cancellationToken)
    {
        return ParaResposta(await service.PausarAsync(identifier, cancellationToken));
    }

    private static async Task<Results<Ok<TarefaCicloResponse>, ProblemHttpResult>> BloquearAsync(
        Guid identifier,
        ITarefaService service,
        CancellationToken cancellationToken)
    {
        return ParaResposta(await service.BloquearAsync(identifier, cancellationToken));
    }

    private static async Task<Results<Ok<TarefaCicloResponse>, ProblemHttpResult>> DesbloquearAsync(
        Guid identifier,
        ITarefaService service,
        CancellationToken cancellationToken)
    {
        return ParaResposta(await service.DesbloquearAsync(identifier, cancellationToken));
    }

    private static async Task<Results<Created<BranchResponse>, ProblemHttpResult>> AdicionarBranchAsync(
        Guid identifier,
        AdicionarBranchRequest request,
        ITarefaService service,
        CancellationToken cancellationToken)
    {
        var resultado = await service.AdicionarBranchAsync(identifier, request, cancellationToken);

        return resultado.Status switch
        {
            TarefaResultadoStatus.NaoEncontrada => NaoEncontrada(),
            TarefaResultadoStatus.BranchRepetida => TypedResults.Problem(detail: BranchRepetida, statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.Created((string?)null, resultado.Branch),
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoverBranchAsync(
        Guid identifier,
        Guid branchIdentifier,
        ITarefaService service,
        CancellationToken cancellationToken)
    {
        var resultado = await service.RemoverBranchAsync(identifier, branchIdentifier, cancellationToken);
        if (resultado.Status == TarefaResultadoStatus.NaoEncontrada)
        {
            return TypedResults.Problem(detail: "Tarefa ou branch não encontrada.", statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ConclusaoPreviaResponse>, ProblemHttpResult>> ObterPreviaAsync(
        Guid identifier,
        IConclusaoService service,
        CancellationToken cancellationToken)
    {
        var previa = await service.ObterPreviaAsync(identifier, cancellationToken);
        if (previa is null)
        {
            return NaoEncontrada();
        }

        return TypedResults.Ok(previa);
    }

    private static async Task<Results<Ok<TarefaCicloResponse>, ProblemHttpResult>> ConcluirAsync(
        Guid identifier,
        IConclusaoService service,
        CancellationToken cancellationToken)
    {
        return ParaResposta(await service.ConcluirAsync(identifier, cancellationToken));
    }

    private static Results<Ok<TarefaCicloResponse>, ProblemHttpResult> ParaResposta(TarefaResultado resultado)
    {
        if (resultado.Status == TarefaResultadoStatus.NaoEncontrada)
        {
            return NaoEncontrada();
        }

        return TypedResults.Ok(resultado.Tarefa);
    }

    private static ProblemHttpResult NaoEncontrada()
    {
        return TypedResults.Problem(detail: TarefaNaoEncontrada, statusCode: StatusCodes.Status404NotFound);
    }
}
