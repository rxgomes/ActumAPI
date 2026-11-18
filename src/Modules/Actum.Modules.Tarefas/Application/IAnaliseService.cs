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

    /// <summary>
    /// The sources of the analysis are invalid; the messages per field are in <see cref="AnaliseResultado.Erros"/>
    /// </summary>
    FontesInvalidas,
}

/// <summary>
/// Result of an analysis operation. Business rule violations are not results: they throw
/// <see cref="Actum.Shared.Abstractions.Domain.DomainException"/>.
/// </summary>
/// <param name="Status">Outcome</param>
/// <param name="Analise">Analysis created, for a successful analysis</param>
/// <param name="Item">Item after the marking, for a successful marking</param>
/// <param name="Erros">
/// Messages in pt-BR per field (<c>Fontes</c>, <c>Anexos</c>, <c>Prompt</c>), for <see cref="AnaliseResultadoStatus.FontesInvalidas"/>
/// </param>
public sealed record AnaliseResultado(
    AnaliseResultadoStatus Status,
    AnaliseResponse? Analise = null,
    AnaliseItemResponse? Item = null,
    IReadOnlyDictionary<string, string[]>? Erros = null)
{
    /// <summary>
    /// No task (or item of the task) has the given identifier
    /// </summary>
    public static readonly AnaliseResultado NaoEncontrada = new(AnaliseResultadoStatus.NaoEncontrada);

    /// <summary>
    /// An analysis of the same task is still running
    /// </summary>
    public static readonly AnaliseResultado EmAndamento = new(AnaliseResultadoStatus.EmAndamento);

    /// <summary>
    /// The sources of the analysis are invalid
    /// </summary>
    /// <param name="erros">Messages in pt-BR per field</param>
    public static AnaliseResultado FontesInvalidas(IReadOnlyDictionary<string, string[]> erros) =>
        new(AnaliseResultadoStatus.FontesInvalidas, Erros: erros);
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
    /// Bring the comments up to date from the Asana, then send to the AI the company, title and projects plus the
    /// chosen sources (description, comments with the rework history, attachments downloaded now from the Asana
    /// and the prompt written by the user) and store the answer, with the sources, as the new current version.
    /// Without sources, the description and the comments are sent. Items equal to items marked in the previous
    /// version come already marked. Fails with <see cref="Actum.Shared.Abstractions.Domain.DomainException"/> for
    /// an archived task and propagates the Asana and Claude API failures without storing anything.
    /// </summary>
    /// <param name="tarefaIdentifier">Task identifier</param>
    /// <param name="fontes">Sources of the analysis; <see langword="null"/> for description and comments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The new version, <see cref="AnaliseResultadoStatus.NaoEncontrada"/>,
    /// <see cref="AnaliseResultadoStatus.EmAndamento"/> when another analysis of the task is running or
    /// <see cref="AnaliseResultadoStatus.FontesInvalidas"/> with the messages per field (nothing sent to the AI)
    /// </returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-18</modified>
    Task<AnaliseResultado> AnalisarAsync(Guid tarefaIdentifier, FontesDaAnaliseRequest? fontes, CancellationToken cancellationToken);

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
