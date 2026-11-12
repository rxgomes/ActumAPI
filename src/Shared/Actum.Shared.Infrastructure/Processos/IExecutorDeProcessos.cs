namespace Actum.Shared.Infrastructure.Processos;

/// <summary>
/// Runs external programs (git, dotnet, npm) as child processes of the API, without a shell, with a time limit and
/// with an environment that does not carry the settings of the Actum itself (<c>ASPNETCORE_*</c>, <c>Cors__*</c>).
/// </summary>
/// <author>Ricardo Gomes</author>
/// <created>2026-11-12</created>
/// <modified>2026-11-12</modified>
public interface IExecutorDeProcessos
{
    /// <summary>
    /// Run a program until it exits and capture its output. A non-zero exit code is not an error: it is returned in
    /// the result for the caller to interpret.
    /// </summary>
    /// <param name="comando">Program, arguments, working directory, time limit and extra environment variables</param>
    /// <param name="cancellationToken">Cancellation token; when cancelled, the process and its children are killed</param>
    /// <returns>Exit code, standard output and standard error</returns>
    /// <exception cref="TempoEsgotadoException">The program did not exit within the time limit (it was killed)</exception>
    /// <exception cref="ExecutavelNaoEncontradoException">The program could not be started (not found in the <c>PATH</c>)</exception>
    /// <author>Ricardo Gomes</author>
    /// <created>2026-11-12</created>
    /// <modified>2026-11-12</modified>
    Task<ResultadoDoProcesso> ExecutarAsync(ComandoExterno comando, CancellationToken cancellationToken);
}
