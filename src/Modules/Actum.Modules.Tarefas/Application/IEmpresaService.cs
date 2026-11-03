using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Query and adjustment of the companies (Asana workspaces). Companies are created only by the synchronization.
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-03</created>
/// <modified>2026-11-03</modified>
public interface IEmpresaService
{
    /// <summary>
    /// List every company, ordered by name
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Companies</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<IReadOnlyList<EmpresaResponse>> ListarAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Change the color and/or whether the company is synchronized; fields not sent are kept
    /// </summary>
    /// <param name="identifier">Company identifier</param>
    /// <param name="request">Fields to change</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The changed company, or <see langword="null"/> when no company has the identifier</returns>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-03</created>
    /// <modified>2026-11-03</modified>
    Task<EmpresaResponse?> AlterarAsync(Guid identifier, AlterarEmpresaRequest request, CancellationToken cancellationToken);
}
