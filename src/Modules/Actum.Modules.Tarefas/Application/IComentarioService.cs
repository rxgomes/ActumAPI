using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Comments, attachments and prints sent from the Actum to the Asana task, with mentions. Business rule violations
/// throw <see cref="Actum.Shared.Abstractions.Domain.DomainException"/> with a pt-BR message; Asana failures throw
/// <see cref="Actum.Modules.Asana.Errors.AsanaException"/>, or <see cref="EnvioParcialException"/> when some files
/// were already attached.
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface IComentarioService
{
    /// <summary>
    /// List the people following the task in the Asana (except the user of the token), suggesting who reopened the
    /// task most recently or, without reopening, who last commented
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The participants, or <see langword="null"/> when the task does not exist in the Actum</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<IReadOnlyList<ParticipanteResponse>?> ListarParticipantesAsync(Guid tarefaIdentifier, CancellationToken cancellationToken);

    /// <summary>
    /// Attach the files to the Asana task, in order, and publish a comment with the mentions, the text and the
    /// list of attachments; the comment is recorded in the task as read. Text or at least one file is required;
    /// up to 10 files, 25 MB each and 50 MB in total; mentions must be participants of the task.
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="texto">Comment text (optional)</param>
    /// <param name="mencoes">Asana identifiers of the participants to mention</param>
    /// <param name="arquivos">Files to attach</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The comment and the attachment names, or <see langword="null"/> when the task does not exist in the Actum</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<ComentarioEnviadoResponse?> EnviarAsync(
        Guid tarefaIdentifier,
        string? texto,
        IReadOnlyList<string> mencoes,
        IReadOnlyList<ArquivoParaEnviar> arquivos,
        CancellationToken cancellationToken);
}
