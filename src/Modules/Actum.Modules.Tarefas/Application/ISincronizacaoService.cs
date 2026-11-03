using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// On-demand synchronization of the local task copy with the Asana tasks assigned to the user
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface ISincronizacaoService
{
    /// <summary>
    /// Synchronize every company with synchronization turned on: create the companies of new workspaces, create and
    /// update tasks, copy comments, detect reopenings and archive the tasks that left the open list of the user.
    /// Each company is saved on its own, so the companies already processed stay saved when a later one fails.
    /// The run is recorded with its totals and outcome.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The summary of the run, or <see cref="SincronizacaoResultado.EmAndamento"/> when another run is in progress</returns>
    /// <exception cref="Actum.Modules.Asana.Errors.AsanaException">The Asana failed; the run is recorded as failed before the exception propagates</exception>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<SincronizacaoResultado> SincronizarAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Get the most recent synchronization run
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The most recent run, or <see langword="null"/> when there was none</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<SincronizacaoResponse?> ObterUltimaAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of a synchronization request
/// </summary>
public enum SincronizacaoResultadoStatus
{
    /// <summary>
    /// The synchronization ran
    /// </summary>
    Concluida,

    /// <summary>
    /// Another synchronization is in progress; nothing was done
    /// </summary>
    EmAndamento,
}

/// <summary>
/// Result of a synchronization request: the run summary, or the reason it did not run
/// </summary>
/// <param name="Status">Outcome</param>
/// <param name="Sincronizacao">Run summary, present only when <paramref name="Status"/> is <see cref="SincronizacaoResultadoStatus.Concluida"/></param>
public sealed record SincronizacaoResultado(SincronizacaoResultadoStatus Status, SincronizacaoResponse? Sincronizacao = null)
{
    /// <summary>
    /// Another synchronization is in progress
    /// </summary>
    public static readonly SincronizacaoResultado EmAndamento = new(SincronizacaoResultadoStatus.EmAndamento);

    /// <summary>
    /// The synchronization ran
    /// </summary>
    /// <param name="sincronizacao">Run summary</param>
    /// <returns>Result with the summary</returns>
    public static SincronizacaoResultado Concluida(SincronizacaoResponse sincronizacao)
    {
        return new SincronizacaoResultado(SincronizacaoResultadoStatus.Concluida, sincronizacao);
    }
}
