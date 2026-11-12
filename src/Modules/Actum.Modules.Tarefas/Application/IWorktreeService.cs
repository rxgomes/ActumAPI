namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Local worktrees of the implementation branches of a task: creation (or a new attempt) and removal
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-12</created>
/// <modified>2026-11-12</modified>
public interface IWorktreeService
{
    /// <summary>
    /// Create the worktrees of the branch that are not created yet, or create again the ones of a removed worktree, in
    /// the same folder. Idempotent: parts already created are kept. A failure in a part is recorded in it and returned
    /// in the branch, not thrown.
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="branchIdentifier">Branch identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The task and the branch with its worktree, or <see cref="TarefaResultadoStatus.NaoEncontrada"/> when
    /// the task or the branch does not exist</returns>
    /// <exception cref="Actum.Shared.Abstractions.Domain.DomainException">The branch is a base, or the company has no
    /// worktrees folder or no clone of a part of the branch</exception>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task<TarefaResultado> CriarAsync(Guid identifier, Guid branchIdentifier, CancellationToken cancellationToken);

    /// <summary>
    /// Remove the worktrees of the branch without forcing. Nothing is removed when a part has changes not committed or
    /// commits not pushed.
    /// </summary>
    /// <param name="identifier">Task identifier</param>
    /// <param name="branchIdentifier">Branch identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The task and the branch with its removed worktree;
    /// <see cref="TarefaResultadoStatus.WorktreeComPendencias"/> with the pending items per part; or
    /// <see cref="TarefaResultadoStatus.NaoEncontrada"/> when the task or the branch does not exist or the branch has no
    /// active worktree</returns>
    /// <exception cref="Actum.Shared.Abstractions.Domain.DomainException">The git could not remove a folder (file in
    /// use)</exception>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task<TarefaResultado> RemoverAsync(Guid identifier, Guid branchIdentifier, CancellationToken cancellationToken);
}
