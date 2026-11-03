namespace Actum.Modules.Claude.Errors;

/// <summary>
/// Failure analyzing a demand with the Claude API. The message is in pt-BR and is meant to be shown to the user.
/// </summary>
public abstract class ClaudeException : Exception
{
    /// <summary>
    /// Create the exception with a pt-BR message
    /// </summary>
    /// <param name="message">Failure description, in pt-BR</param>
    /// <param name="innerException">Original failure, if any</param>
    protected ClaudeException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The Claude API key (<c>Claude:ApiKey</c>) is not configured
/// </summary>
public sealed class ClaudeNaoConfiguradoException()
    : ClaudeException("A chave da Claude API não está configurada. Configure-a com dotnet user-secrets set \"Claude:ApiKey\" \"<chave>\" no projeto Actum.Api.");

/// <summary>
/// The Anthropic rejected the API key (<c>401</c>) or denied access (<c>403</c>)
/// </summary>
/// <param name="innerException">Original failure</param>
public sealed class ClaudeChaveInvalidaException(Exception? innerException = null)
    : ClaudeException("A chave da Claude API é inválida ou não tem permissão para usar o modelo configurado.", innerException);

/// <summary>
/// The Anthropic account has no credits to use the API (retrying does not help)
/// </summary>
/// <param name="innerException">Original failure</param>
public sealed class ClaudeSemCreditoException(Exception? innerException = null)
    : ClaudeException("A conta da Anthropic está sem créditos para usar a Claude API. Adicione créditos em Plans & Billing, no Console da Anthropic, e tente novamente.", innerException);

/// <summary>
/// The model declined to analyze the demand (<c>stop_reason</c> = <c>refusal</c>)
/// </summary>
public sealed class ClaudeRecusaException()
    : ClaudeException("A IA não analisou esta demanda: a solicitação foi recusada pelo modelo. Revise o conteúdo da tarefa ou tente novamente mais tarde.");

/// <summary>
/// The answer was cut off (<c>max_tokens</c>) or did not follow the expected JSON format
/// </summary>
/// <param name="innerException">Original failure, if any</param>
public sealed class ClaudeRespostaIncompletaException(Exception? innerException = null)
    : ClaudeException("A IA devolveu uma resposta incompleta. Tente analisar novamente.", innerException);

/// <summary>
/// The Anthropic could not be reached or did not answer (network failure, request limit, overload or <c>5xx</c>)
/// </summary>
/// <param name="innerException">Original failure, if any</param>
public sealed class ClaudeIndisponivelException(Exception? innerException = null)
    : ClaudeException("Não foi possível comunicar com a Claude API. Tente novamente em instantes.", innerException);
