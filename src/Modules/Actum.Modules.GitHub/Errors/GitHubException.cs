namespace Actum.Modules.GitHub.Errors;

/// <summary>
/// Failure communicating with the GitHub. The message is in pt-BR and is meant to be shown to the user.
/// </summary>
public abstract class GitHubException : Exception
{
    /// <summary>
    /// Create the exception with a pt-BR message
    /// </summary>
    /// <param name="message">Failure description, in pt-BR</param>
    /// <param name="innerException">Original failure, if any</param>
    protected GitHubException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The GitHub token (<c>GitHub:Token</c>) is not configured
/// </summary>
public sealed class GitHubNaoConfiguradoException()
    : GitHubException("O token do GitHub não está configurado. Configure-o com dotnet user-secrets set \"GitHub:Token\" \"<token>\" no projeto Actum.Api.");

/// <summary>
/// The GitHub rejected the token (<c>401</c>)
/// </summary>
public sealed class GitHubTokenRecusadoException()
    : GitHubException("O token do GitHub foi recusado. Verifique se ele é válido e não expirou.");

/// <summary>
/// The repository does not exist or the token has no access to it (<c>403</c>/<c>404</c>)
/// </summary>
/// <param name="repositorio">Repository (<c>owner/repo</c>)</param>
public sealed class GitHubRepositorioInacessivelException(string repositorio)
    : GitHubException($"O repositório {repositorio} não existe no GitHub ou o token não tem acesso a ele (permissão Contents: read and write).");

/// <summary>
/// The GitHub rejected the creation of a branch for a reason other than the branch already existing
/// </summary>
/// <param name="repositorio">Repository (<c>owner/repo</c>)</param>
/// <param name="branch">Branch name</param>
public sealed class GitHubBranchRecusadaException(string repositorio, string branch)
    : GitHubException($"O GitHub recusou a criação da branch '{branch}' no repositório {repositorio}.");

/// <summary>
/// The GitHub could not be reached or did not answer as expected (network failure, timeout, <c>5xx</c> or
/// request limit exhausted)
/// </summary>
/// <param name="innerException">Original failure, if any</param>
public sealed class GitHubIndisponivelException(Exception? innerException = null)
    : GitHubException("Não foi possível comunicar com o GitHub. Tente novamente em instantes.", innerException);
