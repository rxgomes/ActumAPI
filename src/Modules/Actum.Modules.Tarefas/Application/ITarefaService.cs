using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Lifecycle actions of a task that change only the Actum (nothing is sent to the Asana): start, block, unblock,
/// internal due date, priority and branches. Business rule violations throw
/// <see cref="Actum.Shared.Abstractions.Domain.DomainException"/> with a pt-BR message.
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface ITarefaService
{
    /// <summary>
    /// Start a task from the inbox with its base and implementation branches (registered as not sent, with the
    /// parts) and the internal due date. Before saving, the implementation branch is created in the GitHub
    /// repository of each part from the last commit of the base (an existing branch is accepted); the
    /// <see cref="Actum.Modules.GitHub.Errors.GitHubException"/> failures leave the task unchanged.
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="request">Branches, parts and internal due date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The started task, <see cref="TarefaResultadoStatus.NaoEncontrada"/>, or
    /// <see cref="TarefaResultadoStatus.BranchRepetida"/> when the task already has a branch with the implementation
    /// name (a base branch already registered is reused)
    /// </returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-06</modified>
    Task<TarefaResultado> IniciarAsync(Guid identifier, IniciarTarefaRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// List up to 3 distinct names of base branches registered in the tasks of the same company as the given task,
    /// from the most recent to the oldest
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Base branch names, or <see langword="null"/> when the task does not exist</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-06</created>
    /// <modified>2026-11-06</modified>
    Task<IReadOnlyList<string>?> ListarBasesRecentesAsync(Guid identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Resume work on a paused task already started or in rework (it becomes <c>Em andamento</c>). With a new
    /// branch, the implementation branch is created in the GitHub repository of each part from the last commit of
    /// the base and registered linked to the open rework, before saving (the base is reused when the task already
    /// has it); the <see cref="Actum.Modules.GitHub.Errors.GitHubException"/> failures leave the task unchanged.
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="request">New branch to create, or <see langword="null"/> to only resume</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The task in progress, <see cref="TarefaResultadoStatus.NaoEncontrada"/>, or
    /// <see cref="TarefaResultadoStatus.BranchRepetida"/> when the task already has a branch with the implementation name
    /// </returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-04</created>
    /// <modified>2026-11-06</modified>
    Task<TarefaResultado> RetomarAsync(Guid identifier, RetomarTarefaRequest? request, CancellationToken cancellationToken);

    /// <summary>
    /// Pause a task in progress (it becomes <c>Pausada</c>)
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The paused task, or <see cref="TarefaResultadoStatus.NaoEncontrada"/></returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-04</created>
    /// <modified>2026-11-04</modified>
    Task<TarefaResultado> PausarAsync(Guid identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Block a task in progress or in rework, keeping the status to return to
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The blocked task, or <see cref="TarefaResultadoStatus.NaoEncontrada"/></returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<TarefaResultado> BloquearAsync(Guid identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Unblock a task, returning it to the status it had before blocking
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The unblocked task, or <see cref="TarefaResultadoStatus.NaoEncontrada"/></returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<TarefaResultado> DesbloquearAsync(Guid identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Change the internal due date and/or the priority of a task that is not archived. Setting the due date of a
    /// reopened task resolves its pending due date.
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="request">Fields to change; fields not sent are kept</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The changed task, or <see cref="TarefaResultadoStatus.NaoEncontrada"/></returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<TarefaResultado> AlterarAsync(Guid identifier, AlterarTarefaRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Register a branch in a task in progress, blocked or in rework, linked to the open rework when there is one.
    /// Before saving, a base branch must exist in the GitHub repository of each part, and an implementation branch
    /// is created in each of them from the most recent base branch of the task (an existing branch is accepted).
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="request">Branch role, name and parts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The task and the new branch, <see cref="TarefaResultadoStatus.NaoEncontrada"/>, or
    /// <see cref="TarefaResultadoStatus.BranchRepetida"/> when the task already has a branch with the name
    /// </returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-06</modified>
    Task<TarefaResultado> AdicionarBranchAsync(Guid identifier, AdicionarBranchRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Remove a branch not yet sent to the Asana
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="branchIdentifier">Branch identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The task after the removal, or <see cref="TarefaResultadoStatus.NaoEncontrada"/> for the task or the branch</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<TarefaResultado> RemoverBranchAsync(Guid identifier, Guid branchIdentifier, CancellationToken cancellationToken);
}
