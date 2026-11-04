namespace Actum.Modules.Asana.Errors;

/// <summary>
/// Failure communicating with the Asana. The message is in pt-BR and is meant to be shown to the user.
/// </summary>
public abstract class AsanaException : Exception
{
    /// <summary>
    /// Create the exception with a pt-BR message
    /// </summary>
    /// <param name="message">Failure description, in pt-BR</param>
    /// <param name="innerException">Original failure, if any</param>
    protected AsanaException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The Asana token (<c>Asana:Token</c>) is not configured
/// </summary>
public sealed class AsanaNaoConfiguradoException()
    : AsanaException("O token do Asana não está configurado. Configure-o com dotnet user-secrets set \"Asana:Token\" \"<token>\" no projeto Actum.Api.");

/// <summary>
/// The Asana rejected the token (<c>401</c>) or denied access to the resource (<c>403</c>)
/// </summary>
public sealed class AsanaAcessoNegadoException()
    : AsanaException("O token do Asana é inválido ou não tem acesso ao recurso solicitado.");

/// <summary>
/// The Asana could not be reached or did not answer as expected (network failure, timeout, <c>5xx</c>
/// or request limit exhausted)
/// </summary>
/// <param name="innerException">Original failure, if any</param>
public sealed class AsanaIndisponivelException(Exception? innerException = null)
    : AsanaException("Não foi possível comunicar com o Asana. Tente novamente em instantes.", innerException);

/// <summary>
/// The Asana rejected a write (comment or attachment) with <c>400</c>, <c>404</c>, <c>413</c> or <c>422</c>;
/// retrying the same request does not help
/// </summary>
/// <param name="message">Failure description, in pt-BR</param>
public sealed class AsanaRequisicaoRecusadaException(string message)
    : AsanaException(message);
