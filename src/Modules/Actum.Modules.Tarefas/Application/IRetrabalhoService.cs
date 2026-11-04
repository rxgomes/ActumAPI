using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Outcome of a reprocessing request
/// </summary>
/// <param name="EmAndamento">Whether a synchronization or another reprocessing was running (nothing was done)</param>
/// <param name="Resumo">Summary, present when the reprocessing ran</param>
public sealed record ReprocessamentoResultado(bool EmAndamento, ReprocessamentoResponse? Resumo = null);

/// <summary>
/// Rework indicators (dashboard) and reprocessing of the Asana history that feeds them. A rework is a completion
/// followed later by "marcou como não concluída" in the Asana history.
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface IRetrabalhoService
{
    /// <summary>
    /// Read the Asana history of every task and recalculate its completions and reworks. A task whose history
    /// cannot be read keeps its reworks and is reported as a failure; the others go on. Runs one at a time with the
    /// synchronization.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The summary, or <see cref="ReprocessamentoResultado.EmAndamento"/> when a synchronization or reprocessing is running</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<ReprocessamentoResultado> ReprocessarAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Rework indicators of the period (by reopening date), per company and in total: reworks, tasks with rework,
    /// completed tasks, rework rate, monthly evolution and the ranking of the tasks with more reworks
    /// </summary>
    /// <param name="de">First day of the period</param>
    /// <param name="ate">Last day of the period</param>
    /// <param name="empresa">Company identifier; all companies when <see langword="null"/></param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The indicators, or <see langword="null"/> when the company does not exist</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<IndicadoresResponse?> ObterIndicadoresAsync(DateOnly de, DateOnly ate, Guid? empresa, CancellationToken cancellationToken);
}
