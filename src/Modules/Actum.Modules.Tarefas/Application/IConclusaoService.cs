using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Completion of a task: adds the branch tags not yet sent to the Asana task and completes it in the Asana and in
/// the Actum. Safe to repeat after a partial failure: tags already sent are not added again.
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface IConclusaoService
{
    /// <summary>
    /// Preview the completion: the tags that will be added (the tag text of the branches not yet sent) and whether
    /// the completion is allowed, with the reason when it is not. Does not call the Asana.
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The preview, or <see langword="null"/> when no task has the identifier</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-06</modified>
    Task<ConclusaoPreviaResponse?> ObterPreviaAsync(Guid identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Complete a task in progress, blocked or in rework that has a base and an implementation branch: add to the
    /// Asana task, as tags with the tag text of each branch (<see cref="Domain.TarefaBranch.Tag"/>), only the
    /// branches not yet sent (each one saved as sent right after), complete the task in the Asana and then in the
    /// Actum, resolving the open rework
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The completed task, or <see cref="TarefaResultadoStatus.NaoEncontrada"/></returns>
    /// <exception cref="Actum.Shared.Abstractions.Domain.DomainException">
    /// The task cannot be completed (status or branches), or it no longer exists in the Asana; nothing is written
    /// </exception>
    /// <exception cref="Actum.Modules.Asana.Errors.AsanaException">
    /// The Asana failed; the tags sent until then stay recorded and the local status is unchanged
    /// </exception>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-06</modified>
    Task<TarefaResultado> ConcluirAsync(Guid identifier, CancellationToken cancellationToken);
}
