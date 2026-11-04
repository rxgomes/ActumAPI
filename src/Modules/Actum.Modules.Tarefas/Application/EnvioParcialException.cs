using Actum.Modules.Asana.Errors;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Some files were already attached to the Asana task when a later step (another file or the comment) failed.
/// The message, in pt-BR, says what was sent so the user does not send it again.
/// </summary>
public sealed class EnvioParcialException : Exception
{
    /// <summary>
    /// Create the exception from the failure of the later step
    /// </summary>
    /// <param name="anexosEnviados">Names of the files already attached</param>
    /// <param name="falha">Failure of the later step</param>
    public EnvioParcialException(IReadOnlyList<string> anexosEnviados, AsanaException falha)
        : base($"{falha.Message} Os anexos {string.Join(", ", anexosEnviados)} já foram enviados ao Asana; envie de novo só o que faltou.", falha)
    {
        AnexosEnviados = anexosEnviados;
        Falha = falha;
    }

    /// <summary>
    /// Names of the files already attached to the Asana task
    /// </summary>
    public IReadOnlyList<string> AnexosEnviados { get; }

    /// <summary>
    /// Failure of the later step (defines the HTTP status: <c>400</c> rejected or <c>502</c> unavailable)
    /// </summary>
    public AsanaException Falha { get; }
}
