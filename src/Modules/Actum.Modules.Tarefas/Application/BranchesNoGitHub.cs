using Actum.Modules.GitHub.Application;
using Actum.Modules.Tarefas.Domain;
using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Checks and creates the branches of a task in the GitHub repositories of its parts. Every repository and every
/// base branch is checked before anything is created, so a typo in the base leaves no branch behind.
/// </summary>
internal sealed class BranchesNoGitHub(IGitHubClient gitHub)
{
    /// <summary>
    /// Create the implementation branch in the repository of each part, from the last commit of the base branch.
    /// A branch that already exists in a repository is accepted (retry after a partial failure).
    /// </summary>
    /// <exception cref="DomainException">A part has no repository in the company, or the base does not exist in a repository</exception>
    public async Task CriarImplementacaoAsync(
        Empresa empresa,
        IReadOnlyList<ParteBranch> partes,
        string branchBase,
        string branchImplementacao,
        CancellationToken cancellationToken)
    {
        var commitsDaBase = await ConferirBaseAsync(empresa, partes, branchBase, cancellationToken);

        foreach (var (repositorio, sha) in commitsDaBase)
        {
            await gitHub.CriarBranchAsync(repositorio, branchImplementacao, sha, cancellationToken);
        }
    }

    /// <summary>
    /// Check that the base branch exists in the repository of each part
    /// </summary>
    /// <returns>Repository of each part with the last commit of the base branch in it</returns>
    /// <exception cref="DomainException">A part has no repository in the company, or the base does not exist in a repository</exception>
    public async Task<IReadOnlyList<(string Repositorio, string Sha)>> ConferirBaseAsync(
        Empresa empresa,
        IReadOnlyList<ParteBranch> partes,
        string branchBase,
        CancellationToken cancellationToken)
    {
        // Repositórios primeiro: uma parte sem repositório não chega a chamar o GitHub.
        var repositorios = partes.Select(parte => (Parte: parte, Repositorio: Repositorio(empresa, parte))).ToList();

        var commits = new List<(string Repositorio, string Sha)>(repositorios.Count);
        foreach (var (parte, repositorio) in repositorios)
        {
            var sha = await gitHub.ObterShaDaBranchAsync(repositorio, branchBase, cancellationToken)
                ?? throw new DomainException($"A branch base '{branchBase}' não existe no repositório de {Descrever(parte)} ({repositorio}).");

            commits.Add((repositorio, sha));
        }

        return commits;
    }

    private static string Repositorio(Empresa empresa, ParteBranch parte)
    {
        return empresa.Repositorio(parte)
            ?? throw new DomainException($"A empresa {empresa.Nome} não tem repositório de {Descrever(parte)} no GitHub. Cadastre-o na página Empresas.");
    }

    private static string Descrever(ParteBranch parte)
    {
        return parte == ParteBranch.Front ? "front" : "back";
    }
}
