using Actum.Modules.Tarefas.Contracts;
using Actum.Shared.Abstractions.Pagination;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Task queries for the front: the grouped board, the paged list and the task detail, plus marking the comments
/// of a task as read
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface IPainelService
{
    /// <summary>
    /// Get the open tasks (neither completed nor archived) grouped by what to do first: reopened, inbox, late,
    /// today, this week (until Sunday), later and without internal due date. Each task is in a single group and
    /// every group is returned, even when empty.
    /// </summary>
    /// <param name="filtro">Company and text filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The board</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<PainelResponse> ObterPainelAsync(PainelFiltro filtro, CancellationToken cancellationToken);

    /// <summary>
    /// List the tasks of any status, filtered by company, status and text, most recent first
    /// </summary>
    /// <param name="filtro">Filters and page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The requested page with the total number of tasks that match the filters</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<PagedResult<CartaoTarefaResponse>> ListarAsync(ListarTarefasFiltro filtro, CancellationToken cancellationToken);

    /// <summary>
    /// Get the task detail with branches, reworks (each with the comments of its window) and comments
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The detail, or <see langword="null"/> when no task has the identifier</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<TarefaDetalheResponse?> ObterDetalheAsync(Guid identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Mark every comment of the task as read
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns><see langword="false"/> when no task has the identifier</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<bool> MarcarComentariosLidosAsync(Guid identifier, CancellationToken cancellationToken);
}
