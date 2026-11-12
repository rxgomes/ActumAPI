using Actum.Modules.Git.Errors;

namespace Actum.Modules.Git.Application;

/// <summary>
/// Git command line over the local clones of the user. Paths are absolute. Every operation fails with
/// <see cref="GitNaoEncontradoException"/> when the git is not installed, <see cref="GitTempoEsgotadoException"/>
/// when the command exceeds its time limit and <see cref="GitComandoRecusadoException"/> when the git refuses it
/// (unless stated otherwise). Commands never wait for user interaction (credential prompts fail at once).
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-12</created>
/// <modified>2026-11-12</modified>
public interface IGitLocal
{
    /// <summary>
    /// Get the URL of the <c>origin</c> remote of a clone
    /// </summary>
    /// <param name="clone">Folder of the clone (root of the repository)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>URL of <c>origin</c>, or <see langword="null"/> when the folder does not exist, is not the root of a
    /// git repository or has no <c>origin</c> remote</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task<string?> ObterOrigemAsync(string clone, CancellationToken cancellationToken);

    /// <summary>
    /// Update <c>origin/&lt;branch&gt;</c> in the clone from the <c>origin</c> remote, even in single-branch clones.
    /// Fails with <see cref="GitRemotoInacessivelException"/> when the remote cannot be reached or does not have the
    /// branch.
    /// </summary>
    /// <param name="clone">Folder of the clone</param>
    /// <param name="branch">Branch name (may contain <c>/</c>)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task BuscarBranchAsync(string clone, string branch, CancellationToken cancellationToken);

    /// <summary>
    /// List the worktrees registered in the clone, including the clone itself
    /// </summary>
    /// <param name="clone">Folder of the clone</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Full path, branch checked out (<see langword="null"/> when detached) and whether the folder no longer
    /// exists, for each worktree</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task<IReadOnlyList<WorktreeDoClone>> ListarWorktreesAsync(string clone, CancellationToken cancellationToken);

    /// <summary>
    /// Create a worktree of a branch in a folder. When the local branch does not exist, it is created from
    /// <c>origin/&lt;branch&gt;</c> with it as upstream (call <see cref="BuscarBranchAsync"/> first); otherwise the
    /// local branch is used. Runs one at a time per clone.
    /// </summary>
    /// <param name="clone">Folder of the clone</param>
    /// <param name="caminho">Folder of the new worktree (must not exist or be empty)</param>
    /// <param name="branch">Branch name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task AdicionarWorktreeAsync(string clone, string caminho, string branch, CancellationToken cancellationToken);

    /// <summary>
    /// Check whether a file is ignored by the git in the clone (a tracked file is never ignored)
    /// </summary>
    /// <param name="clone">Folder of the clone</param>
    /// <param name="arquivo">File path relative to the root of the clone</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns><see langword="true"/> when the file is ignored</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task<bool> EhIgnoradoAsync(string clone, string arquivo, CancellationToken cancellationToken);

    /// <summary>
    /// Count what would be lost by removing a worktree: changes not committed (including new files that are not
    /// ignored) and local commits not pushed (compared with the upstream or, without upstream, with every branch of
    /// <c>origin</c>)
    /// </summary>
    /// <param name="worktree">Folder of the worktree</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of changes and of commits not pushed</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task<PendenciasDoWorktree> ObterPendenciasAsync(string worktree, CancellationToken cancellationToken);

    /// <summary>
    /// Remove a worktree without forcing: the git refuses when there are changes or files in use. Runs one at a time
    /// per clone.
    /// </summary>
    /// <param name="clone">Folder of the clone</param>
    /// <param name="caminho">Folder of the worktree</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task RemoverWorktreeAsync(string clone, string caminho, CancellationToken cancellationToken);

    /// <summary>
    /// Remove from the clone the registration of worktrees whose folders no longer exist. Runs one at a time per clone.
    /// </summary>
    /// <param name="clone">Folder of the clone</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task LimparWorktreesOrfaosAsync(string clone, CancellationToken cancellationToken);
}

/// <summary>
/// Worktree registered in a clone
/// </summary>
/// <param name="Caminho">Full path of the folder</param>
/// <param name="Branch">Branch checked out (without <c>refs/heads/</c>); <see langword="null"/> when detached</param>
/// <param name="PastaAusente">Whether the folder no longer exists (the git reports it as prunable)</param>
public sealed record WorktreeDoClone(string Caminho, string? Branch, bool PastaAusente);

/// <summary>
/// What would be lost by removing a worktree
/// </summary>
/// <param name="Alteracoes">Changed, removed or new (not ignored) files</param>
/// <param name="CommitsNaoEnviados">Local commits that are not in the remote</param>
public sealed record PendenciasDoWorktree(int Alteracoes, int CommitsNaoEnviados)
{
    /// <summary>
    /// Whether there is nothing to lose
    /// </summary>
    public bool Limpo => Alteracoes == 0 && CommitsNaoEnviados == 0;
}
