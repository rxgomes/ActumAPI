using Actum.Modules.GitHub.Errors;

namespace Actum.Modules.GitHub.Application;

/// <summary>
/// Access to the GitHub REST API on behalf of the user owning the configured token (<c>GitHub:Token</c>).
/// Repositories are given as <c>owner/repo</c>. Every operation fails with
/// <see cref="GitHubNaoConfiguradoException"/> when the token is not configured,
/// <see cref="GitHubTokenRecusadoException"/> when the GitHub rejects it,
/// <see cref="GitHubRepositorioInacessivelException"/> when the repository does not exist or is not accessible and
/// <see cref="GitHubIndisponivelException"/> when the GitHub cannot be reached.
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-06</created>
/// <modified>2026-11-06</modified>
public interface IGitHubClient
{
    /// <summary>
    /// Get the SHA of the last commit of a branch
    /// </summary>
    /// <param name="repositorio">Repository (<c>owner/repo</c>)</param>
    /// <param name="branch">Branch name (may contain <c>/</c>)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Commit SHA, or <see langword="null"/> when the branch does not exist in the repository</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-06</created>
    /// <modified>2026-11-06</modified>
    Task<string?> ObterShaDaBranchAsync(string repositorio, string branch, CancellationToken cancellationToken);

    /// <summary>
    /// Create a branch pointing to a commit. A branch that already exists is not changed and is not an error.
    /// Fails with <see cref="GitHubBranchRecusadaException"/> when the GitHub rejects the creation for another reason.
    /// </summary>
    /// <param name="repositorio">Repository (<c>owner/repo</c>)</param>
    /// <param name="branch">Name of the new branch</param>
    /// <param name="sha">Commit SHA the branch points to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns><see langword="true"/> when the branch was created; <see langword="false"/> when it already existed</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-06</created>
    /// <modified>2026-11-06</modified>
    Task<bool> CriarBranchAsync(string repositorio, string branch, string sha, CancellationToken cancellationToken);
}
