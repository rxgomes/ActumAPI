namespace Actum.Modules.Git.Errors;

/// <summary>
/// Failure running the git in a local clone. The message is in pt-BR and is meant to be shown to the user.
/// </summary>
public abstract class GitException : Exception
{
    /// <summary>
    /// Create the exception with a pt-BR message
    /// </summary>
    /// <param name="message">Failure description, in pt-BR</param>
    /// <param name="innerException">Original failure, if any</param>
    protected GitException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Maximum length of the git error kept in the message
    /// </summary>
    internal const int ErroMaxLength = 500;

    /// <summary>
    /// Git error trimmed to fit in a message
    /// </summary>
    internal static string Resumir(string erro)
    {
        var texto = erro.Trim();
        if (texto.Length == 0)
        {
            return "sem mensagem do git";
        }

        return texto.Length <= ErroMaxLength ? texto : string.Concat(texto.AsSpan(0, ErroMaxLength), "…");
    }
}

/// <summary>
/// The <c>git</c> executable was not found on the PC
/// </summary>
/// <param name="innerException">Original failure</param>
public sealed class GitNaoEncontradoException(Exception? innerException = null)
    : GitException("O git não foi encontrado no PC. Instale o Git for Windows e confira se ele está no PATH do usuário que executa o Actum.", innerException);

/// <summary>
/// A git command did not finish within its time limit and was killed
/// </summary>
/// <param name="operacao">Description of the operation, in pt-BR (e.g. "buscar a branch x")</param>
/// <param name="limite">Time limit</param>
/// <param name="innerException">Original failure</param>
public sealed class GitTempoEsgotadoException(string operacao, TimeSpan limite, Exception? innerException = null)
    : GitException($"O git não conseguiu {operacao} em {limite.TotalSeconds:0} segundos e foi interrompido.", innerException);

/// <summary>
/// The branch could not be fetched from the <c>origin</c> remote (network, credential or branch missing in the remote)
/// </summary>
/// <param name="branch">Branch name</param>
/// <param name="erro">Error printed by the git</param>
public sealed class GitRemotoInacessivelException(string branch, string erro)
    : GitException($"Não foi possível buscar a branch '{branch}' no remoto origin: {Resumir(erro)}");

/// <summary>
/// The git refused a command (branch checked out elsewhere, folder in use, invalid path, etc.)
/// </summary>
/// <param name="operacao">Description of the operation, in pt-BR (e.g. "criar o worktree em E:\wt\x\front")</param>
/// <param name="erro">Error printed by the git</param>
public sealed class GitComandoRecusadoException(string operacao, string erro)
    : GitException($"O git não conseguiu {operacao}: {Resumir(erro)}");
