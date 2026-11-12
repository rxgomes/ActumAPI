using Microsoft.Extensions.Configuration;

namespace Actum.Modules.Git.Application;

/// <summary>
/// Settings of the <c>Git</c> configuration section, with defaults. Read on every command, so a change in the
/// configuration is picked up without restart.
/// </summary>
/// <param name="Executavel">Git executable (name in the <c>PATH</c> or full path)</param>
/// <param name="TempoLimiteBusca">Time limit of a fetch</param>
/// <param name="TempoLimiteWorktree">Time limit of a worktree creation (checkout of the files)</param>
/// <param name="TempoLimite">Time limit of the other commands</param>
internal sealed record GitOptions(string Executavel, TimeSpan TempoLimiteBusca, TimeSpan TempoLimiteWorktree, TimeSpan TempoLimite)
{
    public const string Secao = "Git";

    public const string ExecutavelPadrao = "git";

    public const int TempoLimiteBuscaPadrao = 60;

    public const int TempoLimiteWorktreePadrao = 120;

    public const int TempoLimitePadrao = 15;

    /// <summary>
    /// Read the <c>Git</c> section, applying the defaults (time limits in seconds)
    /// </summary>
    public static GitOptions Ler(IConfiguration configuration)
    {
        var secao = configuration.GetSection(Secao);
        var executavel = secao["Executavel"];

        return new GitOptions(
            string.IsNullOrWhiteSpace(executavel) ? ExecutavelPadrao : executavel.Trim(),
            Segundos(secao["TempoLimiteBuscaSegundos"], TempoLimiteBuscaPadrao),
            Segundos(secao["TempoLimiteWorktreeSegundos"], TempoLimiteWorktreePadrao),
            Segundos(secao["TempoLimiteSegundos"], TempoLimitePadrao));
    }

    private static TimeSpan Segundos(string? valor, int padrao)
    {
        return TimeSpan.FromSeconds(int.TryParse(valor, out var segundos) && segundos > 0 ? segundos : padrao);
    }
}
