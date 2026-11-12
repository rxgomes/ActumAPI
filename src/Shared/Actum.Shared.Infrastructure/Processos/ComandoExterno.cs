namespace Actum.Shared.Infrastructure.Processos;

/// <summary>
/// External program to run: each argument is passed as is (no shell, no quoting)
/// </summary>
/// <param name="Executavel">Program name (looked up in the <c>PATH</c>) or full path</param>
/// <param name="Argumentos">Arguments, one per item</param>
/// <param name="TempoLimite">Time limit; the process and its children are killed when it is exceeded</param>
/// <param name="PastaDeTrabalho">Working directory; <see langword="null"/> keeps the one of the API</param>
/// <param name="Variaveis">Environment variables added to (or replacing) the inherited ones</param>
public sealed record ComandoExterno(
    string Executavel,
    IReadOnlyList<string> Argumentos,
    TimeSpan TempoLimite,
    string? PastaDeTrabalho = null,
    IReadOnlyDictionary<string, string>? Variaveis = null)
{
    /// <summary>
    /// Command line for logs and messages (arguments with spaces in quotes)
    /// </summary>
    public string Descricao => string.Join(' ', new[] { Executavel }.Concat(Argumentos.Select(Citar)));

    private static string Citar(string argumento)
    {
        return argumento.Length == 0 || argumento.Any(char.IsWhiteSpace) ? $"\"{argumento}\"" : argumento;
    }
}

/// <summary>
/// Result of a program that ran until it exited
/// </summary>
/// <param name="CodigoDeSaida">Exit code (<c>0</c> usually means success)</param>
/// <param name="Saida">Standard output</param>
/// <param name="Erro">Standard error</param>
public sealed record ResultadoDoProcesso(int CodigoDeSaida, string Saida, string Erro)
{
    /// <summary>
    /// Whether the exit code is <c>0</c>
    /// </summary>
    public bool Sucesso => CodigoDeSaida == 0;
}

/// <summary>
/// The program did not exit within the time limit and was killed
/// </summary>
/// <param name="comando">Command that was running</param>
public sealed class TempoEsgotadoException(ComandoExterno comando)
    : Exception($"O comando '{comando.Descricao}' não terminou em {comando.TempoLimite.TotalSeconds:0} segundos e foi encerrado.")
{
    /// <summary>
    /// Command that was running
    /// </summary>
    public ComandoExterno Comando { get; } = comando;
}

/// <summary>
/// The program could not be started, usually because it is not in the <c>PATH</c>
/// </summary>
/// <param name="comando">Command that was being started</param>
/// <param name="innerException">Original failure</param>
public sealed class ExecutavelNaoEncontradoException(ComandoExterno comando, Exception innerException)
    : Exception($"Não foi possível executar '{comando.Executavel}': {innerException.Message}", innerException)
{
    /// <summary>
    /// Command that was being started
    /// </summary>
    public ComandoExterno Comando { get; } = comando;
}
