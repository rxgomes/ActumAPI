using Actum.Modules.GitHub.Application;
using Actum.Modules.GitHub.Errors;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// <see cref="IGitHubClient"/> em memória: as branches de cada repositório com o SHA do último commit. Registra as
/// chamadas feitas e permite simular falha na criação de uma branch num repositório.
/// </summary>
public sealed class GitHubClientFalso : IGitHubClient
{
    private readonly Lock _trava = new();

    /// <summary>
    /// Branches de cada repositório (<c>dono/repo</c>), com o SHA do último commit
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Repositorios { get; } = [];

    /// <summary>
    /// Chamadas recebidas, na ordem: <c>sha:repo:branch</c> e <c>criar:repo:branch:sha</c>
    /// </summary>
    public List<string> Chamadas { get; } = [];

    /// <summary>
    /// Repositório cuja criação de branch falha com <see cref="GitHubIndisponivelException"/>
    /// </summary>
    public string? FalharNaCriacaoEm { get; set; }

    /// <summary>
    /// Inclui uma branch num repositório (criando o repositório se for preciso)
    /// </summary>
    public GitHubClientFalso ComBranch(string repositorio, string branch, string sha)
    {
        lock (_trava)
        {
            BranchesDo(repositorio)[branch] = sha;
        }

        return this;
    }

    public Task<string?> ObterShaDaBranchAsync(string repositorio, string branch, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            Chamadas.Add($"sha:{repositorio}:{branch}");

            if (!Repositorios.TryGetValue(repositorio, out var branches))
            {
                throw new GitHubRepositorioInacessivelException(repositorio);
            }

            return Task.FromResult(branches.GetValueOrDefault(branch));
        }
    }

    public Task<bool> CriarBranchAsync(string repositorio, string branch, string sha, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            Chamadas.Add($"criar:{repositorio}:{branch}:{sha}");

            if (repositorio == FalharNaCriacaoEm)
            {
                throw new GitHubIndisponivelException();
            }

            var branches = BranchesDo(repositorio);
            if (branches.ContainsKey(branch))
            {
                return Task.FromResult(false);
            }

            branches[branch] = sha;
            return Task.FromResult(true);
        }
    }

    private Dictionary<string, string> BranchesDo(string repositorio)
    {
        if (!Repositorios.TryGetValue(repositorio, out var branches))
        {
            branches = new Dictionary<string, string>(StringComparer.Ordinal);
            Repositorios[repositorio] = branches;
        }

        return branches;
    }
}
