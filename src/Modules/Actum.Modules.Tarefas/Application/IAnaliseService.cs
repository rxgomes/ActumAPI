using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Outcome of an analysis operation
/// </summary>
public enum AnaliseResultadoStatus
{
    /// <summary>
    /// The operation was applied
    /// </summary>
    Sucesso,

    /// <summary>
    /// No task (or item of the task) has the given identifier
    /// </summary>
    NaoEncontrada,

    /// <summary>
    /// An analysis of the same task is still running
    /// </summary>
    EmAndamento,
}

/// <summary>
/// Result of an analysis operation. Business rule violations are not results: they throw
/// <see cref="Actum.Shared.Abstractions.Domain.DomainException"/>.
/// </summary>
/// <param name="Status">Outcome</param>
/// <param name="Analise">Analysis created, for a successful analysis</param>
/// <param name="Item">Item after the marking, for a successful marking</param>
public sealed record AnaliseResultado(AnaliseResultadoStatus Status, AnaliseResponse? Analise = null, AnaliseItemResponse? Item = null)
{
    /// <summary>
    /// No task (or item of the task) has the given identifier
    /// </summary>
    public static readonly AnaliseResultado NaoEncontrada = new(AnaliseResultadoStatus.NaoEncontrada);

    /// <summary>
    /// An analysis of the same task is still running
    /// </summary>
    public static readonly AnaliseResultado EmAndamento = new(AnaliseResultadoStatus.EmAndamento);
}

/// <summary>
/// AI analysis of the task demand (objective, checklist and questions), stored in versions with markable items.
/// Nothing is sent to the Asana.
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface IAnaliseService
{
    /// <summary>
    /// Send the full task context (company, title, description, projects, comments and rework history) to the
    /// AI and store the answer as the new current version. Items equal to items marked in the previous version
    /// come already marked. Fails with <see cref="Actum.Shared.Abstractions.Domain.DomainException"/> for an
    /// archived task and propagates the Claude API failures without storing anything.
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The new version, <see cref="AnaliseResultadoStatus.NaoEncontrada"/> or
    /// <see cref="AnaliseResultadoStatus.EmAndamento"/> when another analysis of the task is running
    /// </returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<AnaliseResultado> AnalisarAsync(Guid tarefaIdentifier, CancellationToken cancellationToken);

    /// <summary>
    /// List every analysis version of the task, most recent first, with its items in order
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The versions (empty when never analyzed), or <see langword="null"/> when the task does not exist</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<IReadOnlyList<AnaliseResponse>?> ListarAsync(Guid tarefaIdentifier, CancellationToken cancellationToken);

    /// <summary>
    /// Mark (done/resolved) or unmark an item of the current version of the task analysis. Fails with
    /// <see cref="Actum.Shared.Abstractions.Domain.DomainException"/> for an item of a previous version.
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="itemIdentifier">Item identifier</param>
    /// <param name="concluido">Whether the item is done/resolved</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The item after the marking, or <see cref="AnaliseResultadoStatus.NaoEncontrada"/> when the item does not belong to the task</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<AnaliseResultado> MarcarItemAsync(Guid tarefaIdentifier, Guid itemIdentifier, bool concluido, CancellationToken cancellationToken);
}
