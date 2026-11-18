using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Files attached to the task in the Asana, read on demand (nothing is stored in the Actum). Asana failures throw
/// <see cref="Actum.Modules.Asana.Errors.AsanaException"/>; a task deleted in the Asana throws
/// <see cref="Actum.Shared.Abstractions.Domain.DomainException"/> with a pt-BR message.
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-13</created>
/// <modified>2026-11-13</modified>
public interface IAnexoService
{
    /// <summary>
    /// List the files attached to the task (in the task itself or in its comments), oldest first
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The attachments, or <see langword="null"/> when the task does not exist in the Actum</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-13</created>
    /// <modified>2026-11-13</modified>
    Task<IReadOnlyList<AnexoResponse>?> ListarAsync(Guid tarefaIdentifier, CancellationToken cancellationToken);

    /// <summary>
    /// Get a fresh address to download a file attached to the task (valid for a few minutes) or, for external files,
    /// the address to open it. Fails with a <see cref="Actum.Shared.Abstractions.Domain.DomainException"/> when the
    /// Asana offers no address for the file.
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="anexoGid">Attachment identifier in the Asana</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The address, or <see langword="null"/> when the task does not exist in the Actum or the attachment does not
    /// exist or is not attached to this task
    /// </returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-13</created>
    /// <modified>2026-11-13</modified>
    Task<Uri?> ObterEnderecoDeDownloadAsync(Guid tarefaIdentifier, string anexoGid, CancellationToken cancellationToken);
}
