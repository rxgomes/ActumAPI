using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Errors;

namespace Actum.Modules.Asana.Application;

/// <summary>
/// Access to the Asana API on behalf of the user owning the configured token (<c>Asana:Token</c>).
/// Every operation fails with <see cref="AsanaNaoConfiguradoException"/> when the token is not configured,
/// <see cref="AsanaAcessoNegadoException"/> when the Asana rejects it and
/// <see cref="AsanaIndisponivelException"/> when the Asana cannot be reached.
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface IAsanaClient
{
    /// <summary>
    /// Get the authenticated user and the workspaces they can access
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authenticated user with their workspaces</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<AsanaUsuario> ObterUsuarioAtualAsync(CancellationToken cancellationToken);

    /// <summary>
    /// List every open (not completed) task assigned to the authenticated user in a workspace, across all pages
    /// </summary>
    /// <param name="workspaceGid">Workspace identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Open tasks assigned to the user</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<IReadOnlyList<AsanaTarefa>> ListarTarefasAbertasAsync(string workspaceGid, CancellationToken cancellationToken);

    /// <summary>
    /// Get a task by its identifier
    /// </summary>
    /// <param name="tarefaGid">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The task, or <see langword="null"/> when it does not exist (deleted)</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<AsanaTarefa?> ObterTarefaAsync(string tarefaGid, CancellationToken cancellationToken);

    /// <summary>
    /// List the history (comments and events) of a task, across all pages, in the Asana order
    /// </summary>
    /// <param name="tarefaGid">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task stories</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<IReadOnlyList<AsanaStory>> ListarStoriesAsync(string tarefaGid, CancellationToken cancellationToken);

    /// <summary>
    /// Get the workspace tag whose name is exactly the given one (case sensitive), creating it when it does not exist
    /// </summary>
    /// <param name="workspaceGid">Workspace identifier</param>
    /// <param name="nome">Exact tag name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Existing or created tag</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<AsanaTag> ObterOuCriarTagAsync(string workspaceGid, string nome, CancellationToken cancellationToken);

    /// <summary>
    /// Add a tag to a task
    /// </summary>
    /// <param name="tarefaGid">Task identifier</param>
    /// <param name="tagGid">Tag identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task AdicionarTagAsync(string tarefaGid, string tagGid, CancellationToken cancellationToken);

    /// <summary>
    /// Mark a task as completed
    /// </summary>
    /// <param name="tarefaGid">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task ConcluirTarefaAsync(string tarefaGid, CancellationToken cancellationToken);

    /// <summary>
    /// List the followers of a task (who created it, is assigned to it or commented on it), the people that can be
    /// mentioned in a comment
    /// </summary>
    /// <param name="tarefaGid">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Followers with identifier and name, or <see langword="null"/> when the task does not exist (deleted)</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<IReadOnlyList<AsanaUsuarioResumo>?> ListarParticipantesAsync(string tarefaGid, CancellationToken cancellationToken);

    /// <summary>
    /// Attach a file to a task. Fails with <see cref="AsanaRequisicaoRecusadaException"/> when the Asana rejects the
    /// file or the task no longer exists; never repeats an upload the Asana already received.
    /// </summary>
    /// <param name="tarefaGid">Task identifier</param>
    /// <param name="nome">File name shown in the Asana</param>
    /// <param name="tipo">Content type (e.g. <c>image/png</c>)</param>
    /// <param name="conteudo">File content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Attachment created</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<AsanaAnexo> AnexarArquivoAsync(string tarefaGid, string nome, string tipo, Stream conteudo, CancellationToken cancellationToken);

    /// <summary>
    /// Publish a comment on a task, mentioning the given users at the beginning (they are notified by the Asana).
    /// The text is published exactly as typed, with its line breaks. Fails with
    /// <see cref="AsanaRequisicaoRecusadaException"/> when the Asana rejects it or the task no longer exists.
    /// </summary>
    /// <param name="tarefaGid">Task identifier</param>
    /// <param name="texto">Comment text</param>
    /// <param name="mencoes">Identifiers of the users to mention</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comment created, with identifier, local date and plain text</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<AsanaStory> ComentarAsync(string tarefaGid, string texto, IReadOnlyList<string> mencoes, CancellationToken cancellationToken);
}
