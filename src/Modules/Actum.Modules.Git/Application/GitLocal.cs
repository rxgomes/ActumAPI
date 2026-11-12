using System.Globalization;
using Actum.Modules.Git.Errors;
using Actum.Shared.Infrastructure.Processos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Git.Application;

/// <summary>
/// <see cref="IGitLocal"/> over the <c>git</c> command line. Only porcelain output is parsed (messages may be
/// translated); the error output goes to the log and to the exception message.
/// </summary>
internal sealed class GitLocal(
    IExecutorDeProcessos executor,
    TravasDosClones travas,
    IConfiguration configuration,
    ILogger<GitLocal> logger) : IGitLocal
{
    /// <summary>
    /// Every command fails instead of asking for a credential nobody would see
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Ambiente = new Dictionary<string, string>
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GCM_INTERACTIVE"] = "never",
    };

    public async Task<string?> ObterOrigemAsync(string clone, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(clone))
        {
            return null;
        }

        var opcoes = GitOptions.Ler(configuration);

        var raiz = await ExecutarAsync(opcoes, clone, ["rev-parse", "--show-toplevel"], opcoes.TempoLimite, "ler o repositório", cancellationToken);
        if (!raiz.Sucesso || !MesmoCaminho(raiz.Saida.Trim(), clone))
        {
            return null;
        }

        var origem = await ExecutarAsync(opcoes, clone, ["remote", "get-url", "origin"], opcoes.TempoLimite, "ler o remoto origin", cancellationToken);
        var url = origem.Saida.Trim();

        return origem.Sucesso && url.Length > 0 ? url : null;
    }

    public async Task BuscarBranchAsync(string clone, string branch, CancellationToken cancellationToken)
    {
        var opcoes = GitOptions.Ler(configuration);

        // Refspec explícito: atualiza origin/<branch> mesmo em clones --single-branch.
        var resultado = await ExecutarAsync(
            opcoes,
            clone,
            ["fetch", "--no-tags", "origin", $"+refs/heads/{branch}:refs/remotes/origin/{branch}"],
            opcoes.TempoLimiteBusca,
            $"buscar a branch '{branch}'",
            cancellationToken);

        if (!resultado.Sucesso)
        {
            throw new GitRemotoInacessivelException(branch, resultado.Erro);
        }
    }

    public async Task<IReadOnlyList<WorktreeDoClone>> ListarWorktreesAsync(string clone, CancellationToken cancellationToken)
    {
        var opcoes = GitOptions.Ler(configuration);

        var resultado = await ExecutarComSucessoAsync(
            opcoes, clone, ["worktree", "list", "--porcelain"], opcoes.TempoLimite, "listar os worktrees", cancellationToken);

        return LerWorktrees(resultado.Saida);
    }

    public async Task AdicionarWorktreeAsync(string clone, string caminho, string branch, CancellationToken cancellationToken)
    {
        var opcoes = GitOptions.Ler(configuration);

        using var trava = await travas.AdquirirAsync(clone, cancellationToken);

        var local = await ExecutarAsync(
            opcoes, clone, ["show-ref", "--verify", "--quiet", $"refs/heads/{branch}"], opcoes.TempoLimite, "ler as branches", cancellationToken);

        // Sem branch local, -b cria a partir de origin/<branch> já com upstream (branch.autoSetupMerge).
        IReadOnlyList<string> argumentos = local.Sucesso
            ? ["worktree", "add", caminho, branch]
            : ["worktree", "add", "-b", branch, caminho, $"origin/{branch}"];

        await ExecutarComSucessoAsync(
            opcoes, clone, argumentos, opcoes.TempoLimiteWorktree, $"criar o worktree da branch '{branch}' em {caminho}", cancellationToken);

        logger.LogInformation("Worktree da branch {Branch} criado em {Caminho} (clone {Clone})", branch, caminho, clone);
    }

    public async Task<bool> EhIgnoradoAsync(string clone, string arquivo, CancellationToken cancellationToken)
    {
        var opcoes = GitOptions.Ler(configuration);
        var caminho = arquivo.Replace('\\', '/');

        var resultado = await ExecutarAsync(
            opcoes, clone, ["check-ignore", "-q", "--", caminho], opcoes.TempoLimite, $"conferir se {arquivo} é ignorado", cancellationToken);

        return resultado.CodigoDeSaida switch
        {
            0 => true,
            1 => false,
            _ => throw new GitComandoRecusadoException($"conferir se {arquivo} é ignorado", resultado.Erro),
        };
    }

    public async Task<PendenciasDoWorktree> ObterPendenciasAsync(string worktree, CancellationToken cancellationToken)
    {
        var opcoes = GitOptions.Ler(configuration);

        var status = await ExecutarComSucessoAsync(
            opcoes, worktree, ["status", "--porcelain"], opcoes.TempoLimite, $"ler as alterações de {worktree}", cancellationToken);
        var alteracoes = status.Saida.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(linha => linha.Trim().Length > 0);

        var upstream = await ExecutarAsync(
            opcoes, worktree, ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], opcoes.TempoLimite, "ler o upstream", cancellationToken);

        IReadOnlyList<string> contagem = upstream.Sucesso
            ? ["rev-list", "--count", "@{u}..HEAD"]
            : ["rev-list", "--count", "HEAD", "--not", "--remotes=origin"];

        var commits = await ExecutarComSucessoAsync(
            opcoes, worktree, contagem, opcoes.TempoLimite, $"contar os commits não enviados de {worktree}", cancellationToken);

        return new PendenciasDoWorktree(alteracoes, int.Parse(commits.Saida.Trim(), CultureInfo.InvariantCulture));
    }

    public async Task RemoverWorktreeAsync(string clone, string caminho, CancellationToken cancellationToken)
    {
        var opcoes = GitOptions.Ler(configuration);

        using var trava = await travas.AdquirirAsync(clone, cancellationToken);

        await ExecutarComSucessoAsync(
            opcoes, clone, ["worktree", "remove", caminho], opcoes.TempoLimiteWorktree, $"remover o worktree {caminho}", cancellationToken);

        logger.LogInformation("Worktree {Caminho} removido (clone {Clone})", caminho, clone);
    }

    public async Task LimparWorktreesOrfaosAsync(string clone, CancellationToken cancellationToken)
    {
        var opcoes = GitOptions.Ler(configuration);

        using var trava = await travas.AdquirirAsync(clone, cancellationToken);

        await ExecutarComSucessoAsync(
            opcoes, clone, ["worktree", "prune"], opcoes.TempoLimite, "limpar os worktrees sem pasta", cancellationToken);
    }

    /// <summary>
    /// Parse <c>git worktree list --porcelain</c>: blocks separated by a blank line, with <c>worktree &lt;path&gt;</c>,
    /// <c>branch refs/heads/&lt;name&gt;</c> or <c>detached</c>, and <c>prunable</c> when the folder is gone
    /// </summary>
    internal static IReadOnlyList<WorktreeDoClone> LerWorktrees(string saida)
    {
        var worktrees = new List<WorktreeDoClone>();
        string? caminho = null;
        string? branch = null;
        var ausente = false;

        void Fechar()
        {
            if (caminho is not null)
            {
                worktrees.Add(new WorktreeDoClone(caminho, branch, ausente));
            }

            caminho = null;
            branch = null;
            ausente = false;
        }

        foreach (var bruta in saida.Split('\n'))
        {
            var linha = bruta.TrimEnd('\r');
            if (linha.Length == 0)
            {
                Fechar();
            }
            else if (linha.StartsWith("worktree ", StringComparison.Ordinal))
            {
                Fechar();
                caminho = Path.GetFullPath(linha["worktree ".Length..]);
            }
            else if (linha.StartsWith("branch refs/heads/", StringComparison.Ordinal))
            {
                branch = linha["branch refs/heads/".Length..];
            }
            else if (linha == "prunable" || linha.StartsWith("prunable ", StringComparison.Ordinal))
            {
                ausente = true;
            }
        }

        Fechar();

        return worktrees;
    }

    /// <summary>
    /// Whether two paths point to the same folder (the git prints <c>/</c> on Windows; case is ignored)
    /// </summary>
    internal static bool MesmoCaminho(string a, string b)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ResultadoDoProcesso> ExecutarComSucessoAsync(
        GitOptions opcoes,
        string pasta,
        IReadOnlyList<string> argumentos,
        TimeSpan limite,
        string operacao,
        CancellationToken cancellationToken)
    {
        var resultado = await ExecutarAsync(opcoes, pasta, argumentos, limite, operacao, cancellationToken);
        if (!resultado.Sucesso)
        {
            throw new GitComandoRecusadoException(operacao, resultado.Erro);
        }

        return resultado;
    }

    private async Task<ResultadoDoProcesso> ExecutarAsync(
        GitOptions opcoes,
        string pasta,
        IReadOnlyList<string> argumentos,
        TimeSpan limite,
        string operacao,
        CancellationToken cancellationToken)
    {
        // -C em vez da pasta de trabalho: a pasta pode não ser um repositório, e o git diz isso com código de saída.
        var comando = new ComandoExterno(
            opcoes.Executavel,
            ["-c", "core.quotepath=false", "-C", pasta, .. argumentos],
            limite,
            Variaveis: Ambiente);

        ResultadoDoProcesso resultado;
        try
        {
            resultado = await executor.ExecutarAsync(comando, cancellationToken);
        }
        catch (ExecutavelNaoEncontradoException exception)
        {
            throw new GitNaoEncontradoException(exception);
        }
        catch (TempoEsgotadoException exception)
        {
            throw new GitTempoEsgotadoException(operacao, limite, exception);
        }

        if (resultado.Sucesso)
        {
            logger.LogDebug("git {Comando}", comando.Descricao);
        }
        else
        {
            logger.LogDebug("git {Comando} saiu com {Codigo}: {Erro}", comando.Descricao, resultado.CodigoDeSaida, resultado.Erro.Trim());
        }

        return resultado;
    }
}
