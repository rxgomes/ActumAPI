using Actum.Modules.Git.Application;
using Actum.Modules.Git.Errors;

namespace Actum.Modules.Git.Tests;

/// <summary>
/// Cenários da capability <c>git-local</c>, com git real numa pasta temporária
/// </summary>
public sealed class GitLocalTests : IAsyncLifetime
{
    private const string Branch = "feature/ipi-devolvido";

    private RepositorioDeTeste _repositorio = null!;

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private IGitLocal Git => _repositorio.Git;

    private string Clone => _repositorio.Clone;

    public async ValueTask InitializeAsync()
    {
        _repositorio = await RepositorioDeTeste.CriarAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _repositorio.DisposeAsync();
    }

    [Fact]
    public async Task OrigemDoCloneAsync()
    {
        Assert.Equal(_repositorio.Origem, await Git.ObterOrigemAsync(Clone, Cancelamento));
    }

    [Fact]
    public async Task PastaQueNaoEhRepositorioAsync()
    {
        var pasta = Path.Combine(_repositorio.Raiz, "vazia");
        Directory.CreateDirectory(pasta);

        Assert.Null(await Git.ObterOrigemAsync(pasta, Cancelamento));
        Assert.Null(await Git.ObterOrigemAsync(Path.Combine(_repositorio.Raiz, "inexistente"), Cancelamento));
    }

    [Fact]
    public async Task SubpastaDoRepositorioNaoEhCloneAsync()
    {
        var subpasta = Path.Combine(Clone, "src");
        Directory.CreateDirectory(subpasta);

        Assert.Null(await Git.ObterOrigemAsync(subpasta, Cancelamento));
    }

    [Fact]
    public async Task CloneSemOrigemAsync()
    {
        await _repositorio.ExecutarAsync(Clone, "remote", "remove", "origin");

        Assert.Null(await Git.ObterOrigemAsync(Clone, Cancelamento));
    }

    [Fact]
    public async Task BuscaDeBranchNovaAsync()
    {
        await _repositorio.CriarBranchNaOrigemAsync(Branch);

        await Git.BuscarBranchAsync(Clone, Branch, Cancelamento);

        Assert.NotEmpty(await _repositorio.ExecutarAsync(Clone, "rev-parse", "--verify", $"refs/remotes/origin/{Branch}"));
    }

    [Fact]
    public async Task BuscaEmCloneDeUmaBranchSoAsync()
    {
        await _repositorio.CriarBranchNaOrigemAsync(Branch);
        var cloneUnico = Path.Combine(_repositorio.Raiz, "unico");
        await _repositorio.ExecutarAsync(_repositorio.Raiz, "clone", "--single-branch", "-b", "main", _repositorio.Origem, cloneUnico);

        await Git.BuscarBranchAsync(cloneUnico, Branch, Cancelamento);

        Assert.NotEmpty(await _repositorio.ExecutarAsync(cloneUnico, "rev-parse", "--verify", $"refs/remotes/origin/{Branch}"));
    }

    [Fact]
    public async Task BranchInexistenteNoRemotoAsync()
    {
        var excecao = await Assert.ThrowsAsync<GitRemotoInacessivelException>(
            () => Git.BuscarBranchAsync(Clone, "nao-existe", Cancelamento));

        Assert.Contains("'nao-existe'", excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorktreeComBranchLocalInexistenteAsync()
    {
        await _repositorio.CriarBranchNaOrigemAsync(Branch);
        await Git.BuscarBranchAsync(Clone, Branch, Cancelamento);
        var caminho = Path.Combine(_repositorio.Worktrees, "feature-ipi-devolvido", "back");

        await Git.AdicionarWorktreeAsync(Clone, caminho, Branch, Cancelamento);

        Assert.True(File.Exists(Path.Combine(caminho, "README.md")));
        Assert.Equal(Branch, await _repositorio.ExecutarAsync(caminho, "branch", "--show-current"));
        Assert.Equal($"origin/{Branch}", await _repositorio.ExecutarAsync(caminho, "rev-parse", "--abbrev-ref", "@{u}"));

        var worktrees = await Git.ListarWorktreesAsync(Clone, Cancelamento);
        Assert.Contains(new WorktreeDoClone(Path.GetFullPath(caminho), Branch, false), worktrees);
    }

    [Fact]
    public async Task PushSemArgumentosEnviaParaABranchAsync()
    {
        var caminho = await CriarWorktreeAsync("front");
        await File.WriteAllTextAsync(Path.Combine(caminho, "novo.txt"), "x", Cancelamento);
        await _repositorio.ExecutarAsync(caminho, "add", ".");
        await _repositorio.CommitAsync(caminho, "novo");

        await _repositorio.ExecutarAsync(caminho, "push");

        Assert.Equal(
            await _repositorio.ExecutarAsync(caminho, "rev-parse", "HEAD"),
            await _repositorio.ExecutarAsync(_repositorio.Origem, "rev-parse", Branch));
    }

    [Fact]
    public async Task WorktreeComBranchLocalExistenteAsync()
    {
        await _repositorio.CriarBranchNaOrigemAsync(Branch);
        await Git.BuscarBranchAsync(Clone, Branch, Cancelamento);
        await _repositorio.ExecutarAsync(Clone, "branch", Branch, $"origin/{Branch}");
        var caminho = Path.Combine(_repositorio.Worktrees, "x", "back");

        await Git.AdicionarWorktreeAsync(Clone, caminho, Branch, Cancelamento);

        Assert.Equal(Branch, await _repositorio.ExecutarAsync(caminho, "branch", "--show-current"));
    }

    [Fact]
    public async Task BranchEmCheckoutNoCloneAsync()
    {
        await _repositorio.CriarBranchNaOrigemAsync(Branch);
        await Git.BuscarBranchAsync(Clone, Branch, Cancelamento);
        await _repositorio.ExecutarAsync(Clone, "checkout", Branch);

        var excecao = await Assert.ThrowsAsync<GitComandoRecusadoException>(() => Git.AdicionarWorktreeAsync(
            Clone, Path.Combine(_repositorio.Worktrees, "x", "back"), Branch, Cancelamento));

        Assert.StartsWith("O git não conseguiu criar o worktree", excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListagemIncluiOCloneEOsWorktreesAsync()
    {
        var caminho = await CriarWorktreeAsync("front");

        var worktrees = await Git.ListarWorktreesAsync(Clone, Cancelamento);

        Assert.Equal(2, worktrees.Count);
        Assert.Contains(worktrees, worktree => GitLocal.MesmoCaminho(worktree.Caminho, Clone) && worktree.Branch == "main");
        Assert.Contains(worktrees, worktree => GitLocal.MesmoCaminho(worktree.Caminho, caminho) && worktree.Branch == Branch);
    }

    [Fact]
    public async Task ArquivoIgnoradoAsync()
    {
        Assert.True(await Git.EhIgnoradoAsync(Clone, ".env.development", Cancelamento));
        Assert.True(await Git.EhIgnoradoAsync(Clone, @"node_modules\vite\index.js", Cancelamento));
        Assert.False(await Git.EhIgnoradoAsync(Clone, "README.md", Cancelamento));
        Assert.False(await Git.EhIgnoradoAsync(Clone, "appsettings.Development.json", Cancelamento));
    }

    [Fact]
    public async Task PendenciasDeWorktreeLimpoAsync()
    {
        var caminho = await CriarWorktreeAsync("front");

        Assert.Equal(new PendenciasDoWorktree(0, 0), await Git.ObterPendenciasAsync(caminho, Cancelamento));
    }

    [Fact]
    public async Task ArquivosIgnoradosNaoSaoPendenciasAsync()
    {
        var caminho = await CriarWorktreeAsync("front");
        await File.WriteAllTextAsync(Path.Combine(caminho, ".env.development"), "VITE_API_URL=x", Cancelamento);
        Directory.CreateDirectory(Path.Combine(caminho, "node_modules", "vite"));
        await File.WriteAllTextAsync(Path.Combine(caminho, "node_modules", "vite", "index.js"), "x", Cancelamento);

        Assert.True((await Git.ObterPendenciasAsync(caminho, Cancelamento)).Limpo);
    }

    [Fact]
    public async Task AlteracoesNaoCommitadasAsync()
    {
        var caminho = await CriarWorktreeAsync("back");
        await File.AppendAllTextAsync(Path.Combine(caminho, "README.md"), "mudou\n", Cancelamento);
        await File.WriteAllTextAsync(Path.Combine(caminho, "novo.cs"), "x", Cancelamento);

        Assert.Equal(new PendenciasDoWorktree(2, 0), await Git.ObterPendenciasAsync(caminho, Cancelamento));
    }

    [Fact]
    public async Task CommitNaoEnviadoAsync()
    {
        var caminho = await CriarWorktreeAsync("front");
        await File.WriteAllTextAsync(Path.Combine(caminho, "novo.txt"), "x", Cancelamento);
        await _repositorio.ExecutarAsync(caminho, "add", ".");
        await _repositorio.CommitAsync(caminho, "novo");

        Assert.Equal(new PendenciasDoWorktree(0, 1), await Git.ObterPendenciasAsync(caminho, Cancelamento));
    }

    [Fact]
    public async Task CommitNaoEnviadoSemUpstreamAsync()
    {
        var caminho = Path.Combine(_repositorio.Worktrees, "sem-upstream", "front");
        await _repositorio.ExecutarAsync(Clone, "worktree", "add", "-b", "sem-upstream", caminho, "main");
        await File.WriteAllTextAsync(Path.Combine(caminho, "novo.txt"), "x", Cancelamento);
        await _repositorio.ExecutarAsync(caminho, "add", ".");
        await _repositorio.CommitAsync(caminho, "novo");

        Assert.Equal(new PendenciasDoWorktree(0, 1), await Git.ObterPendenciasAsync(caminho, Cancelamento));
    }

    [Fact]
    public async Task RemocaoAsync()
    {
        var caminho = await CriarWorktreeAsync("front");

        await Git.RemoverWorktreeAsync(Clone, caminho, Cancelamento);

        Assert.False(Directory.Exists(caminho));
        Assert.DoesNotContain(await Git.ListarWorktreesAsync(Clone, Cancelamento), worktree => GitLocal.MesmoCaminho(worktree.Caminho, caminho));
    }

    [Fact]
    public async Task RemocaoComAlteracaoEhRecusadaAsync()
    {
        var caminho = await CriarWorktreeAsync("front");
        await File.AppendAllTextAsync(Path.Combine(caminho, "README.md"), "mudou\n", Cancelamento);

        await Assert.ThrowsAsync<GitComandoRecusadoException>(() => Git.RemoverWorktreeAsync(Clone, caminho, Cancelamento));

        Assert.True(Directory.Exists(caminho));
    }

    [Fact]
    public async Task PastaApagadaAMaoAsync()
    {
        var caminho = await CriarWorktreeAsync("front");
        foreach (var arquivo in Directory.EnumerateFiles(caminho, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(arquivo, FileAttributes.Normal);
        }

        Directory.Delete(caminho, recursive: true);

        Assert.Contains(
            await Git.ListarWorktreesAsync(Clone, Cancelamento),
            worktree => GitLocal.MesmoCaminho(worktree.Caminho, caminho) && worktree.PastaAusente);

        await Git.LimparWorktreesOrfaosAsync(Clone, Cancelamento);

        Assert.DoesNotContain(await Git.ListarWorktreesAsync(Clone, Cancelamento), worktree => GitLocal.MesmoCaminho(worktree.Caminho, caminho));

        await Git.AdicionarWorktreeAsync(Clone, caminho, Branch, Cancelamento);
        Assert.True(File.Exists(Path.Combine(caminho, "README.md")));
    }

    [Fact]
    public async Task CriacoesSimultaneasNoMesmoCloneAsync()
    {
        var branches = Enumerable.Range(1, 4).Select(numero => $"feature/paralela-{numero}").ToList();
        foreach (var branch in branches)
        {
            await _repositorio.CriarBranchNaOrigemAsync(branch);
            await Git.BuscarBranchAsync(Clone, branch, Cancelamento);
        }

        await Task.WhenAll(branches.Select(branch => Git.AdicionarWorktreeAsync(
            Clone, Path.Combine(_repositorio.Worktrees, branch.Replace('/', '-'), "front"), branch, Cancelamento)));

        var worktrees = await Git.ListarWorktreesAsync(Clone, Cancelamento);
        Assert.All(branches, branch => Assert.Contains(worktrees, worktree => worktree.Branch == branch));
    }

    [Fact]
    public void LeituraDaListagemPorcelain()
    {
        const string saida = "worktree E:/src/erp\nHEAD 1111\nbranch refs/heads/main\n\n"
            + "worktree E:/wt/aureum/x/front\nHEAD 2222\ndetached\n\n"
            + "worktree E:/wt/aureum/y/front\nHEAD 3333\nbranch refs/heads/feature/y\nprunable gitdir file points to non-existent location\n\n";

        var worktrees = GitLocal.LerWorktrees(saida);

        Assert.Equal(
            [
                new WorktreeDoClone(@"E:\src\erp", "main", false),
                new WorktreeDoClone(@"E:\wt\aureum\x\front", null, false),
                new WorktreeDoClone(@"E:\wt\aureum\y\front", "feature/y", true),
            ],
            worktrees);
    }

    private async Task<string> CriarWorktreeAsync(string parte)
    {
        if (!(await _repositorio.ExecutarAsync(_repositorio.Origem, "branch", "--list", Branch)).Contains(Branch, StringComparison.Ordinal))
        {
            await _repositorio.CriarBranchNaOrigemAsync(Branch);
        }

        await Git.BuscarBranchAsync(Clone, Branch, Cancelamento);
        var caminho = Path.Combine(_repositorio.Worktrees, "feature-ipi-devolvido", parte);
        await Git.AdicionarWorktreeAsync(Clone, caminho, Branch, Cancelamento);

        return caminho;
    }
}
