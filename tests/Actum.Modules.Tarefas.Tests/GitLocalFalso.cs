using Actum.Modules.Git.Application;
using Actum.Modules.Git.Errors;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// <see cref="IGitLocal"/> em memória: clones com origem, branches no remoto, branches em checkout no clone, arquivos
/// ignorados, worktrees registrados e pendências. As pastas dos worktrees são criadas e apagadas de verdade no disco,
/// para a cópia de arquivos funcionar. Registra as chamadas e permite simular falha por alvo e operação.
/// </summary>
public sealed class GitLocalFalso : IGitLocal
{
    private readonly Lock _trava = new();

    private readonly Dictionary<string, CloneFalso> _clones = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<(string Alvo, string Operacao), Exception> _falhas = [];

    /// <summary>
    /// Chamadas recebidas, na ordem: <c>origem:clone</c>, <c>buscar:clone:branch</c>, <c>listar:clone</c>,
    /// <c>adicionar:clone:caminho:branch</c>, <c>ignorado:clone:arquivo</c>, <c>pendencias:caminho</c>,
    /// <c>remover:clone:caminho</c> e <c>limpar:clone</c>
    /// </summary>
    public List<string> Chamadas { get; } = [];

    /// <summary>
    /// Pendências devolvidas para cada worktree (por caminho); sem registro, o worktree está limpo
    /// </summary>
    public Dictionary<string, PendenciasDoWorktree> Pendencias { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registra um clone com a URL do <c>origin</c> e as branches do remoto
    /// </summary>
    public GitLocalFalso ComClone(string clone, string origem, params string[] branchesNoRemoto)
    {
        lock (_trava)
        {
            var falso = new CloneFalso(origem);
            falso.Remotas.UnionWith(branchesNoRemoto);
            _clones[clone] = falso;
        }

        return this;
    }

    /// <summary>
    /// Inclui uma branch no remoto do clone (como o Actum faz no GitHub)
    /// </summary>
    public GitLocalFalso ComBranchNoRemoto(string clone, string branch)
    {
        lock (_trava)
        {
            _clones[clone].Remotas.Add(branch);
        }

        return this;
    }

    /// <summary>
    /// Marca a branch como em checkout no clone principal: a criação do worktree dela é recusada
    /// </summary>
    public GitLocalFalso ComBranchEmCheckout(string clone, string branch)
    {
        lock (_trava)
        {
            _clones[clone].EmCheckout = branch;
        }

        return this;
    }

    /// <summary>
    /// Marca arquivos como ignorados pelo git no clone
    /// </summary>
    public GitLocalFalso ComIgnorados(string clone, params string[] arquivos)
    {
        lock (_trava)
        {
            _clones[clone].Ignorados.UnionWith(arquivos.Select(arquivo => arquivo.Replace('\\', '/')));
        }

        return this;
    }

    /// <summary>
    /// Registra no clone um worktree que já existe (com a pasta criada no disco)
    /// </summary>
    public GitLocalFalso ComWorktree(string clone, string caminho, string branch)
    {
        lock (_trava)
        {
            Directory.CreateDirectory(caminho);
            _clones[clone].Worktrees[caminho] = branch;
        }

        return this;
    }

    /// <summary>
    /// Faz a operação (<c>origem</c>, <c>buscar</c>, <c>listar</c>, <c>adicionar</c>, <c>ignorado</c>,
    /// <c>pendencias</c>, <c>remover</c>, <c>limpar</c>) falhar no clone ou caminho informado; sem exceção, usa
    /// <see cref="GitComandoRecusadoException"/>
    /// </summary>
    public GitLocalFalso FalharEm(string alvo, string operacao, Exception? excecao = null)
    {
        lock (_trava)
        {
            _falhas[(alvo.ToUpperInvariant(), operacao)] = excecao ?? new GitComandoRecusadoException($"{operacao} (simulado)", "falha simulada");
        }

        return this;
    }

    /// <summary>
    /// Desfaz uma falha simulada
    /// </summary>
    public void NaoFalharMais(string alvo, string operacao)
    {
        lock (_trava)
        {
            _falhas.Remove((alvo.ToUpperInvariant(), operacao));
        }
    }

    /// <summary>
    /// Worktrees registrados no clone (caminho → branch)
    /// </summary>
    public IReadOnlyDictionary<string, string> WorktreesDo(string clone)
    {
        lock (_trava)
        {
            return new Dictionary<string, string>(_clones[clone].Worktrees, StringComparer.OrdinalIgnoreCase);
        }
    }

    public Task<string?> ObterOrigemAsync(string clone, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            Registrar(clone, "origem", $"origem:{clone}");
            return Task.FromResult(_clones.TryGetValue(clone, out var falso) ? falso.Origem : null);
        }
    }

    public Task BuscarBranchAsync(string clone, string branch, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            Registrar(clone, "buscar", $"buscar:{clone}:{branch}");
            if (!Clone(clone).Remotas.Contains(branch))
            {
                throw new GitRemotoInacessivelException(branch, $"fatal: couldn't find remote ref refs/heads/{branch}");
            }

            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<WorktreeDoClone>> ListarWorktreesAsync(string clone, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            Registrar(clone, "listar", $"listar:{clone}");
            IReadOnlyList<WorktreeDoClone> lista = Clone(clone).Worktrees
                .Select(worktree => new WorktreeDoClone(worktree.Key, worktree.Value, !Directory.Exists(worktree.Key)))
                .ToList();
            return Task.FromResult(lista);
        }
    }

    public Task AdicionarWorktreeAsync(string clone, string caminho, string branch, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            Registrar(clone, "adicionar", $"adicionar:{clone}:{caminho}:{branch}");
            var falso = Clone(clone);
            if (falso.EmCheckout == branch || falso.Worktrees.ContainsValue(branch))
            {
                throw new GitComandoRecusadoException(
                    $"criar o worktree da branch '{branch}' em {caminho}", $"fatal: '{branch}' is already used by worktree at '{clone}'");
            }

            if (Directory.Exists(caminho) && Directory.EnumerateFileSystemEntries(caminho).Any())
            {
                throw new GitComandoRecusadoException($"criar o worktree da branch '{branch}' em {caminho}", $"fatal: '{caminho}' already exists");
            }

            Directory.CreateDirectory(caminho);
            File.WriteAllText(Path.Combine(caminho, "README.md"), branch);
            falso.Worktrees[caminho] = branch;
            return Task.CompletedTask;
        }
    }

    public Task<bool> EhIgnoradoAsync(string clone, string arquivo, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            Registrar(clone, "ignorado", $"ignorado:{clone}:{arquivo}");
            return Task.FromResult(Clone(clone).Ignorados.Contains(arquivo.Replace('\\', '/')));
        }
    }

    public Task<PendenciasDoWorktree> ObterPendenciasAsync(string worktree, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            Registrar(worktree, "pendencias", $"pendencias:{worktree}");
            return Task.FromResult(Pendencias.GetValueOrDefault(worktree) ?? new PendenciasDoWorktree(0, 0));
        }
    }

    public Task RemoverWorktreeAsync(string clone, string caminho, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            Registrar(clone, "remover", $"remover:{clone}:{caminho}");
            Registrar(caminho, "remover", null);
            var falso = Clone(clone);
            if (!falso.Worktrees.Remove(caminho))
            {
                throw new GitComandoRecusadoException($"remover o worktree {caminho}", $"fatal: '{caminho}' is not a working tree");
            }

            Directory.Delete(caminho, recursive: true);
            return Task.CompletedTask;
        }
    }

    public Task LimparWorktreesOrfaosAsync(string clone, CancellationToken cancellationToken)
    {
        lock (_trava)
        {
            Registrar(clone, "limpar", $"limpar:{clone}");
            var falso = Clone(clone);
            foreach (var caminho in falso.Worktrees.Keys.Where(caminho => !Directory.Exists(caminho)).ToList())
            {
                falso.Worktrees.Remove(caminho);
            }

            return Task.CompletedTask;
        }
    }

    private void Registrar(string alvo, string operacao, string? chamada)
    {
        if (chamada is not null)
        {
            Chamadas.Add(chamada);
        }

        if (_falhas.TryGetValue((alvo.ToUpperInvariant(), operacao), out var excecao))
        {
            throw excecao;
        }
    }

    private CloneFalso Clone(string clone)
    {
        return _clones.TryGetValue(clone, out var falso)
            ? falso
            : throw new GitComandoRecusadoException("ler o repositório", $"fatal: not a git repository: {clone}");
    }

    private sealed class CloneFalso(string origem)
    {
        public string Origem { get; } = origem;

        public HashSet<string> Remotas { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Ignorados { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Worktrees { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? EmCheckout { get; set; }
    }
}
