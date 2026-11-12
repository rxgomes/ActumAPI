using Actum.Modules.Git.Application;
using Actum.Modules.Git.Errors;
using Actum.Modules.Tarefas.Domain;
using Actum.Shared.Abstractions.Domain;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Creates and removes the local worktrees of an implementation branch, in the clones of the company (sibling of
/// <see cref="BranchesNoGitHub"/>). The creation never fails the operation: the error of each part is recorded in it,
/// so the branch in the GitHub and the task stay saved and the creation can be retried.
/// </summary>
internal sealed class WorktreesLocais(IGitLocal git, ILogger<WorktreesLocais> logger)
{
    /// <summary>
    /// Create the worktrees of the parts of the branch that are not created yet (folder of the task:
    /// <c>&lt;pastaWorktrees&gt;\&lt;branch&gt;</c>). Each part is tried independently; a failure is recorded in the
    /// part. Changes the entities only: the caller saves.
    /// </summary>
    /// <returns>The worktree of the branch, or <see langword="null"/> when the branch is a base or the company has
    /// no worktrees folder or no clone of a part of the branch</returns>
    public async Task<Worktree?> CriarAsync(Empresa empresa, TarefaBranch branch, CancellationToken cancellationToken)
    {
        if (branch.Tipo != TipoBranch.Implementacao || !empresa.TemConfiguracaoLocal(branch.Partes))
        {
            return null;
        }

        var worktree = branch.PrepararWorktree(empresa.PastaWorktrees!);

        foreach (var parte in worktree.PartesEmOrdem)
        {
            // Parte criada cuja pasta sumiu (apagada à mão) é refeita.
            if (parte.Situacao == SituacaoWorktree.Criado && Directory.Exists(parte.Caminho))
            {
                continue;
            }

            try
            {
                await CriarParteAsync(empresa, branch.Nome, parte, cancellationToken);
                parte.MarcarCriado(DateTime.Now);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is GitException or FalhaDoWorktree or IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Worktree de {Parte} da branch {Branch} não foi criado: {Erro}", parte.Parte, branch.Nome, exception.Message);
                parte.MarcarFalha(exception.Message);
            }
            catch (Exception exception)
            {
                // A branch no GitHub e a tarefa já foram gravadas: um erro inesperado aqui não vira 500.
                logger.LogError(exception, "Falha inesperada ao criar o worktree de {Parte} da branch {Branch}", parte.Parte, branch.Nome);
                parte.MarcarFalha($"Falha inesperada ao criar o worktree: {exception.Message}");
            }
        }

        return worktree;
    }

    /// <summary>
    /// Remove the worktrees of the parts, without forcing. Nothing is removed when a part has changes not committed or
    /// commits not pushed. Each removed part is saved with <paramref name="salvarAsync"/> as soon as it is done, so a
    /// failure in the second part keeps the first one recorded as removed.
    /// </summary>
    /// <returns>Pending items per part, in pt-BR, when nothing was removed; <see langword="null"/> when removed</returns>
    /// <exception cref="DomainException">The git could not remove a folder (file in use) or the company has no clone
    /// of a part that still has a worktree</exception>
    public async Task<string?> RemoverAsync(
        Empresa empresa,
        Worktree worktree,
        Func<CancellationToken, Task> salvarAsync,
        CancellationToken cancellationToken)
    {
        var partes = new List<(WorktreeParte Parte, string? Clone, bool Registrado)>();
        foreach (var parte in worktree.PartesEmOrdem.Where(parte => parte.Situacao != SituacaoWorktree.Removido))
        {
            var clone = empresa.Clone(parte.Parte);
            var registrado = clone is not null && await RegistradoAsync(clone, parte.Caminho, cancellationToken);
            if (clone is null && Directory.Exists(parte.Caminho))
            {
                throw new DomainException(
                    $"A empresa não tem mais o clone de {Descrever(parte.Parte)} configurado; configure-o na página Empresas para remover o worktree {parte.Caminho}.");
            }

            partes.Add((parte, clone, registrado));
        }

        var pendencias = new List<string>();
        foreach (var (parte, _, registrado) in partes.Where(item => item.Registrado && Directory.Exists(item.Parte.Caminho)))
        {
            var resultado = await git.ObterPendenciasAsync(parte.Caminho, cancellationToken);
            if (!resultado.Limpo)
            {
                pendencias.Add($"{Descrever(parte.Parte)}: {DescreverPendencias(resultado)}");
            }
        }

        if (pendencias.Count > 0)
        {
            return $"O worktree não foi removido para não perder trabalho ({string.Join("; ", pendencias)}). Faça o commit e o push, ou descarte as alterações, e tente de novo.";
        }

        foreach (var (parte, clone, registrado) in partes)
        {
            if (registrado && Directory.Exists(parte.Caminho))
            {
                try
                {
                    await git.RemoverWorktreeAsync(clone!, parte.Caminho, cancellationToken);
                }
                catch (GitComandoRecusadoException exception)
                {
                    throw new DomainException($"{exception.Message} Feche a IDE e pare os servidores da tarefa e tente de novo.");
                }
            }
            else if (registrado)
            {
                await git.LimparWorktreesOrfaosAsync(clone!, cancellationToken);
            }

            // Pasta que não é worktree da tarefa (falha "pasta ocupada") não é tocada.
            parte.MarcarRemovido();
            await salvarAsync(cancellationToken);
        }

        ApagarSeVazia(worktree.Pasta);
        worktree.MarcarRemovido(DateTime.Now);
        await salvarAsync(cancellationToken);

        logger.LogInformation("Worktrees de {Pasta} removidos", worktree.Pasta);

        return null;
    }

    /// <summary>
    /// PowerShell command that opens the Claude Code in the folder of the task (single quotes, <c>'</c> doubled)
    /// </summary>
    public static string ComandoClaude(string pasta)
    {
        return $"cd '{pasta.Replace("'", "''", StringComparison.Ordinal)}'; claude";
    }

    private async Task CriarParteAsync(Empresa empresa, string branch, WorktreeParte parte, CancellationToken cancellationToken)
    {
        var clone = empresa.Clone(parte.Parte)!;

        await git.BuscarBranchAsync(clone, branch, cancellationToken);

        var registrado = (await git.ListarWorktreesAsync(clone, cancellationToken))
            .FirstOrDefault(worktree => MesmoCaminho(worktree.Caminho, parte.Caminho));

        if (registrado is { PastaAusente: true })
        {
            await git.LimparWorktreesOrfaosAsync(clone, cancellationToken);
            registrado = null;
        }

        if (registrado is not null && registrado.Branch != branch)
        {
            throw new FalhaDoWorktree(
                $"A pasta {parte.Caminho} já é o worktree da branch '{registrado.Branch ?? "(sem branch)"}' no clone de {Descrever(parte.Parte)}. Nada foi apagado.");
        }

        if (registrado is null)
        {
            if (Directory.Exists(parte.Caminho) && Directory.EnumerateFileSystemEntries(parte.Caminho).Any())
            {
                throw new FalhaDoWorktree(
                    $"A pasta {parte.Caminho} já existe e não é o worktree da branch '{branch}'. Nada foi apagado; mova ou apague a pasta e tente de novo.");
            }

            await git.AdicionarWorktreeAsync(clone, parte.Caminho, branch, cancellationToken);
        }

        await CopiarArquivosAsync(empresa, clone, parte, cancellationToken);
    }

    private async Task CopiarArquivosAsync(Empresa empresa, string clone, WorktreeParte parte, CancellationToken cancellationToken)
    {
        foreach (var arquivo in empresa.Arquivos(parte.Parte))
        {
            var origem = Path.Combine(clone, arquivo);
            if (!File.Exists(origem))
            {
                logger.LogInformation("Arquivo {Arquivo} não existe no clone {Clone}; não foi copiado", arquivo, clone);
                continue;
            }

            if (!await git.EhIgnoradoAsync(clone, arquivo, cancellationToken))
            {
                throw new FalhaDoWorktree(
                    $"O arquivo {arquivo} não é ignorado pelo git no clone de {Descrever(parte.Parte)}; ele não foi copiado para não aparecer como alteração no worktree.");
            }

            var destino = Path.Combine(parte.Caminho, arquivo);
            if (File.Exists(destino))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            File.Copy(origem, destino);
        }
    }

    private async Task<bool> RegistradoAsync(string clone, string caminho, CancellationToken cancellationToken)
    {
        var worktrees = await git.ListarWorktreesAsync(clone, cancellationToken);
        return worktrees.Any(worktree => MesmoCaminho(worktree.Caminho, caminho));
    }

    private static void ApagarSeVazia(string pasta)
    {
        try
        {
            if (Directory.Exists(pasta) && !Directory.EnumerateFileSystemEntries(pasta).Any())
            {
                Directory.Delete(pasta);
            }
        }
        catch (IOException)
        {
            // Pasta em uso: fica vazia no disco, sem prejuízo.
        }
    }

    private static string DescreverPendencias(PendenciasDoWorktree pendencias)
    {
        var partes = new List<string>(2);
        if (pendencias.Alteracoes > 0)
        {
            partes.Add(pendencias.Alteracoes == 1 ? "1 alteração não commitada" : $"{pendencias.Alteracoes} alterações não commitadas");
        }

        if (pendencias.CommitsNaoEnviados > 0)
        {
            partes.Add(pendencias.CommitsNaoEnviados == 1 ? "1 commit não enviado" : $"{pendencias.CommitsNaoEnviados} commits não enviados");
        }

        return string.Join(" e ", partes);
    }

    private static bool MesmoCaminho(string a, string b)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Descrever(ParteBranch parte)
    {
        return parte == ParteBranch.Front ? "front" : "back";
    }

    /// <summary>
    /// A part could not be created for a reason found by the Actum (folder taken, file not ignored)
    /// </summary>
    private sealed class FalhaDoWorktree(string message) : Exception(message);
}
