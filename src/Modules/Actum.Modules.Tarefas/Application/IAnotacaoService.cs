using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Private notes of a task, kept only in the Actum and never sent to the Asana. Accepted in any status, archived
/// included. An empty text throws <see cref="Actum.Shared.Abstractions.Domain.DomainException"/>.
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface IAnotacaoService
{
    /// <summary>
    /// List the notes of the task in chronological order of creation
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The notes, or <see langword="null"/> when the task does not exist</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<IReadOnlyList<AnotacaoResponse>?> ListarAsync(Guid tarefaIdentifier, CancellationToken cancellationToken);

    /// <summary>
    /// Create a note for the task with the trimmed text
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="texto">Note text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The note created, or <see langword="null"/> when the task does not exist</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<AnotacaoResponse?> CriarAsync(Guid tarefaIdentifier, string texto, CancellationToken cancellationToken);

    /// <summary>
    /// Change the text of a note of the task, recording when it was changed
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="anotacaoIdentifier">Note identifier</param>
    /// <param name="texto">New text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The note changed, or <see langword="null"/> when the note does not belong to the task</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<AnotacaoResponse?> AlterarAsync(Guid tarefaIdentifier, Guid anotacaoIdentifier, string texto, CancellationToken cancellationToken);

    /// <summary>
    /// Delete a note of the task
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="anotacaoIdentifier">Note identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns><see langword="false"/> when the note does not belong to the task</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<bool> ExcluirAsync(Guid tarefaIdentifier, Guid anotacaoIdentifier, CancellationToken cancellationToken);
}
