using Actum.Modules.Git.Application;
using Actum.Modules.Git.Errors;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da capability <c>task-worktrees</c>, com o GitHub e o git simulados e pastas reais numa pasta temporária
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class WorktreesDasTarefasTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP"), IDisposable
{
    private const string Nome = "ipi";

    private const string Main = "main";

    private const string Feature = "261112-ipi/devolvido";

    private const string PastaDaFeature = "261112-ipi-devolvido";

    private const string RepositorioFront = "ixact/erp-web";

    private const string RepositorioBack = "ixact/erp-api";

    private static readonly DateOnly Prazo = new(2026, 11, 20);

    private static readonly IReadOnlyList<ParteBranch> FrontEBack = [ParteBranch.Front, ParteBranch.Back];

    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"actum-wt-{Guid.NewGuid():N}");

    private readonly GitHubClientFalso _gitHub = new GitHubClientFalso()
        .ComBranch(RepositorioFront, Main, "sha-front")
        .ComBranch(RepositorioBack, Main, "sha-back");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string CloneFront => Path.Combine(_raiz, "clones", "erp-web");

    private string CloneBack => Path.Combine(_raiz, "clones", "erp-api");

    private string PastaWorktrees => Path.Combine(_raiz, "Aureum wt");

    private string PastaDaTarefa => Path.Combine(PastaWorktrees, PastaDaFeature);

    private string CaminhoFront => Path.Combine(PastaDaTarefa, "front");

    private string CaminhoBack => Path.Combine(PastaDaTarefa, "back");

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }
    }

    [Fact]
    public async Task InicioComAsDuasPartesCriaOsWorktreesAsync()
    {
        var tarefa = await NaEntradaAsync();

        var resultado = await IniciarAsync(tarefa, FrontEBack);

        var worktree = Implementacao(resultado).Worktree!;
        Assert.Equal(PastaDaTarefa, worktree.Pasta);
        Assert.Equal(SituacaoWorktree.Criado, worktree.Situacao);
        Assert.Equal(
            [(ParteBranch.Front, CaminhoFront, SituacaoWorktree.Criado), (ParteBranch.Back, CaminhoBack, SituacaoWorktree.Criado)],
            worktree.Partes.Select(parte => (parte.Parte, parte.Caminho, parte.Situacao)));
        Assert.All(worktree.Partes, parte => Assert.NotNull(parte.CriadoEm));
        Assert.Equal(Feature, Git.WorktreesDo(CloneFront)[CaminhoFront]);
        Assert.Equal(Feature, Git.WorktreesDo(CloneBack)[CaminhoBack]);
        Assert.Contains($"buscar:{CloneFront}:{Feature}", Git.Chamadas);

        // Gravado: o detalhe lido de novo traz o worktree.
        var detalhe = await DetalheAsync(tarefa);
        Assert.Equal(SituacaoWorktree.Criado, detalhe.Branches.Single(b => b.Tipo == TipoBranch.Implementacao).Worktree!.Situacao);
    }

    [Fact]
    public async Task InicioSoComBackAsync()
    {
        var tarefa = await NaEntradaAsync();

        var resultado = await IniciarAsync(tarefa, [ParteBranch.Back]);

        var worktree = Implementacao(resultado).Worktree!;
        Assert.Equal([ParteBranch.Back], worktree.Partes.Select(parte => parte.Parte));
        Assert.False(Directory.Exists(CaminhoFront));
        Assert.True(Directory.Exists(CaminhoBack));
    }

    [Fact]
    public async Task EmpresaSemConfiguracaoLocalNaoChamaOGitAsync()
    {
        var tarefa = await NaEntradaAsync(configurarLocal: false);

        var resultado = await IniciarAsync(tarefa, FrontEBack);

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        Assert.Null(Implementacao(resultado).Worktree);
        Assert.Empty(Git.Chamadas);
    }

    [Fact]
    public async Task EmpresaSemCloneDeUmaParteNaoCriaNadaAsync()
    {
        var tarefa = await NaEntradaAsync();
        await AlterarEmpresaAsync(empresa => empresa.CloneBack = null);

        var resultado = await IniciarAsync(tarefa, FrontEBack);

        Assert.Null(Implementacao(resultado).Worktree);
        Assert.Empty(Git.Chamadas);
    }

    [Fact]
    public async Task FalhaNumaParteNaoDerrubaOInicioAsync()
    {
        var tarefa = await NaEntradaAsync();
        Git.ComBranchEmCheckout(CloneBack, Feature);

        var resultado = await IniciarAsync(tarefa, FrontEBack);

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        var worktree = Implementacao(resultado).Worktree!;
        Assert.Equal(SituacaoWorktree.Falhou, worktree.Situacao);
        Assert.Equal(SituacaoWorktree.Criado, worktree.Partes[0].Situacao);
        Assert.Equal(SituacaoWorktree.Falhou, worktree.Partes[1].Situacao);
        Assert.Contains("is already used by worktree", worktree.Partes[1].Erro, StringComparison.Ordinal);

        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.EmAndamento, lida!.Status);
        Assert.Contains(_gitHub.Chamadas, chamada => chamada == $"criar:{RepositorioBack}:{Feature}:sha-back");
    }

    [Fact]
    public async Task RemotoInacessivelNaoDerrubaOInicioAsync()
    {
        var tarefa = await NaEntradaAsync();
        Git.FalharEm(CloneFront, "buscar", new GitRemotoInacessivelException(Feature, "fatal: unable to access 'https://github.com/'"))
            .FalharEm(CloneBack, "buscar", new GitTempoEsgotadoException($"buscar a branch '{Feature}'", TimeSpan.FromSeconds(60)));

        var resultado = await IniciarAsync(tarefa, FrontEBack);

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        var worktree = Implementacao(resultado).Worktree!;
        Assert.All(worktree.Partes, parte => Assert.Equal(SituacaoWorktree.Falhou, parte.Situacao));
        Assert.Contains("unable to access", worktree.Partes[0].Erro, StringComparison.Ordinal);
        Assert.Contains("60 segundos", worktree.Partes[1].Erro, StringComparison.Ordinal);
        Assert.Equal(StatusTarefa.EmAndamento, (await LerTarefaAsync(Nome))!.Status);
    }

    [Fact]
    public async Task RetomadaComBranchNovaCriaOutroParAsync()
    {
        var tarefa = await NaEntradaAsync();
        await IniciarAsync(tarefa, FrontEBack);
        await TarefasAsync(service => service.PausarAsync(tarefa, Ct));
        const string Nova = "261120-ipi-ajuste";
        NoRemoto(Nova);

        var resultado = await TarefasAsync(service => service.RetomarAsync(tarefa, new RetomarTarefaRequest(Main, Nova, FrontEBack), Ct));

        var nova = resultado.Tarefa!.Branches.Single(branch => branch.Nome == Nova).Worktree!;
        Assert.Equal(Path.Combine(PastaWorktrees, Nova), nova.Pasta);
        Assert.Equal(SituacaoWorktree.Criado, nova.Situacao);
        var anterior = resultado.Tarefa.Branches.Single(branch => branch.Nome == Feature).Worktree!;
        Assert.Equal(SituacaoWorktree.Criado, anterior.Situacao);
        Assert.True(Directory.Exists(CaminhoFront));
    }

    [Fact]
    public async Task BranchBaseNaoTemWorktreeAsync()
    {
        var tarefa = await NaEntradaAsync();
        await IniciarAsync(tarefa, FrontEBack);
        _gitHub.ComBranch(RepositorioFront, "release/2026-11", "sha-r").ComBranch(RepositorioBack, "release/2026-11", "sha-r");

        var resultado = await TarefasAsync(service => service.AdicionarBranchAsync(tarefa, new AdicionarBranchRequest(TipoBranch.Base, "release/2026-11", FrontEBack), Ct));

        Assert.Null(resultado.Branch!.Worktree);
        var erro = await Assert.ThrowsAsync<DomainException>(() => WorktreeAsync(service => service.CriarAsync(tarefa, resultado.Branch.Identifier, Ct)));
        Assert.Contains("Só branches de implementação", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistroDeImplementacaoCriaOsWorktreesAsync()
    {
        var tarefa = await NaEntradaAsync();
        await IniciarAsync(tarefa, FrontEBack);
        const string Outra = "261113-ipi-front";
        NoRemoto(Outra);

        var resultado = await TarefasAsync(service => service.AdicionarBranchAsync(tarefa, new AdicionarBranchRequest(TipoBranch.Implementacao, Outra, [ParteBranch.Front]), Ct));

        Assert.Equal(SituacaoWorktree.Criado, resultado.Branch!.Worktree!.Situacao);
        Assert.True(Directory.Exists(Path.Combine(PastaWorktrees, Outra, "front")));
    }

    [Fact]
    public async Task RecriarDepoisDeFalhaCriaSoAParteQueFaltavaAsync()
    {
        var (tarefa, branch) = await IniciadaComFalhaNoBackAsync();
        Git.Chamadas.Clear();

        var resultado = await WorktreeAsync(service => service.CriarAsync(tarefa, branch, Ct));

        Assert.Equal(SituacaoWorktree.Criado, resultado.Branch!.Worktree!.Situacao);
        Assert.Contains($"adicionar:{CloneBack}:{CaminhoBack}:{Feature}", Git.Chamadas);
        Assert.DoesNotContain(Git.Chamadas, chamada => chamada.StartsWith($"adicionar:{CloneFront}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecriarComTudoCriadoNaoChamaOGitAsync()
    {
        var tarefa = await NaEntradaAsync();
        var branch = Implementacao(await IniciarAsync(tarefa, FrontEBack)).Identifier;
        Git.Chamadas.Clear();

        var resultado = await WorktreeAsync(service => service.CriarAsync(tarefa, branch, Ct));

        Assert.Equal(SituacaoWorktree.Criado, resultado.Branch!.Worktree!.Situacao);
        Assert.Empty(Git.Chamadas);
    }

    [Fact]
    public async Task PastaJaRegistradaComAMesmaBranchEhConsideradaCriadaAsync()
    {
        var tarefa = await NaEntradaAsync();
        Git.ComWorktree(CloneBack, CaminhoBack, Feature);
        await AlterarEmpresaAsync(empresa => empresa.ArquivosBack = ["appsettings.Development.json"]);
        await File.WriteAllTextAsync(Path.Combine(CloneBack, "appsettings.Development.json"), "{}", Ct);
        Git.ComIgnorados(CloneBack, "appsettings.Development.json");

        var resultado = await IniciarAsync(tarefa, FrontEBack);

        Assert.Equal(SituacaoWorktree.Criado, Implementacao(resultado).Worktree!.Situacao);
        Assert.DoesNotContain(Git.Chamadas, chamada => chamada.StartsWith($"adicionar:{CloneBack}", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(CaminhoBack, "appsettings.Development.json")));
    }

    [Fact]
    public async Task PastaOcupadaNaoEhApagadaAsync()
    {
        var tarefa = await NaEntradaAsync();
        Directory.CreateDirectory(CaminhoFront);
        await File.WriteAllTextAsync(Path.Combine(CaminhoFront, "meu.txt"), "não apagar", Ct);

        var resultado = await IniciarAsync(tarefa, FrontEBack);

        var front = Implementacao(resultado).Worktree!.Partes[0];
        Assert.Equal(SituacaoWorktree.Falhou, front.Situacao);
        Assert.Contains($"A pasta {CaminhoFront} já existe", front.Erro, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(CaminhoFront, "meu.txt")));
    }

    [Fact]
    public async Task PastaRegistradaComOutraBranchAsync()
    {
        var tarefa = await NaEntradaAsync();
        Git.ComWorktree(CloneFront, CaminhoFront, "outra-branch");

        var resultado = await IniciarAsync(tarefa, FrontEBack);

        var front = Implementacao(resultado).Worktree!.Partes[0];
        Assert.Equal(SituacaoWorktree.Falhou, front.Situacao);
        Assert.Contains("'outra-branch'", front.Erro, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PastaApagadaAMaoEhRefeitaAsync()
    {
        var tarefa = await NaEntradaAsync();
        var branch = Implementacao(await IniciarAsync(tarefa, FrontEBack)).Identifier;
        Directory.Delete(CaminhoFront, recursive: true);
        Git.Chamadas.Clear();

        var resultado = await WorktreeAsync(service => service.CriarAsync(tarefa, branch, Ct));

        Assert.Equal(SituacaoWorktree.Criado, resultado.Branch!.Worktree!.Situacao);
        Assert.Equal([$"buscar:{CloneFront}:{Feature}", $"listar:{CloneFront}", $"limpar:{CloneFront}", $"adicionar:{CloneFront}:{CaminhoFront}:{Feature}"], Git.Chamadas);
        Assert.True(Directory.Exists(CaminhoFront));
    }

    [Fact]
    public async Task RecriarComConfiguracaoIncompletaAsync()
    {
        var (tarefa, branch) = await IniciadaComFalhaNoBackAsync();
        await AlterarEmpresaAsync(empresa => empresa.PastaWorktrees = null);

        var erro = await Assert.ThrowsAsync<DomainException>(() => WorktreeAsync(service => service.CriarAsync(tarefa, branch, Ct)));

        Assert.Contains("na página Empresas", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TarefaOuBranchInexistenteAsync()
    {
        var tarefa = await NaEntradaAsync();

        Assert.Equal(TarefaResultadoStatus.NaoEncontrada, (await WorktreeAsync(service => service.CriarAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), Ct))).Status);
        Assert.Equal(TarefaResultadoStatus.NaoEncontrada, (await WorktreeAsync(service => service.CriarAsync(tarefa, Guid.CreateVersion7(), Ct))).Status);
        Assert.Equal(TarefaResultadoStatus.NaoEncontrada, (await WorktreeAsync(service => service.RemoverAsync(tarefa, Guid.CreateVersion7(), Ct))).Status);
    }

    [Fact]
    public async Task CopiaDosArquivosDoFrontAsync()
    {
        var tarefa = await NaEntradaAsync();
        await AlterarEmpresaAsync(empresa => empresa.ArquivosFront = [".env.development", @"config\local.json", "nao-existe.txt"]);
        await File.WriteAllTextAsync(Path.Combine(CloneFront, ".env.development"), "VITE_API_URL=http://localhost:6101", Ct);
        Directory.CreateDirectory(Path.Combine(CloneFront, "config"));
        await File.WriteAllTextAsync(Path.Combine(CloneFront, "config", "local.json"), "{}", Ct);
        Git.ComIgnorados(CloneFront, ".env.development", @"config\local.json");

        var resultado = await IniciarAsync(tarefa, FrontEBack);

        Assert.Equal(SituacaoWorktree.Criado, Implementacao(resultado).Worktree!.Situacao);
        Assert.Equal("VITE_API_URL=http://localhost:6101", await File.ReadAllTextAsync(Path.Combine(CaminhoFront, ".env.development"), Ct));
        Assert.True(File.Exists(Path.Combine(CaminhoFront, "config", "local.json")));
        Assert.False(File.Exists(Path.Combine(CaminhoFront, "nao-existe.txt")));
    }

    [Fact]
    public async Task ArquivoVersionadoNaoEhCopiadoAsync()
    {
        var tarefa = await NaEntradaAsync();
        await AlterarEmpresaAsync(empresa => empresa.ArquivosFront = ["vite.config.ts"]);
        await File.WriteAllTextAsync(Path.Combine(CloneFront, "vite.config.ts"), "export default {}", Ct);

        var resultado = await IniciarAsync(tarefa, FrontEBack);

        var front = Implementacao(resultado).Worktree!.Partes[0];
        Assert.Equal(SituacaoWorktree.Falhou, front.Situacao);
        Assert.Contains("O arquivo vite.config.ts não é ignorado pelo git no clone de front", front.Erro, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(CaminhoFront, "vite.config.ts")));
    }

    [Fact]
    public async Task ArquivoAlteradoNaTarefaNaoEhSobrescritoAsync()
    {
        var (tarefa, branch) = await IniciadaComFalhaNoBackAsync();
        await AlterarEmpresaAsync(empresa => empresa.ArquivosFront = [".env.development"]);
        await File.WriteAllTextAsync(Path.Combine(CloneFront, ".env.development"), "original", Ct);
        Git.ComIgnorados(CloneFront, ".env.development");
        await File.WriteAllTextAsync(Path.Combine(CaminhoFront, ".env.development"), "ajustado na tarefa", Ct);

        // O front está criado: só o back é refeito, e o front não é tocado.
        await WorktreeAsync(service => service.CriarAsync(tarefa, branch, Ct));
        Assert.Equal("ajustado na tarefa", await File.ReadAllTextAsync(Path.Combine(CaminhoFront, ".env.development"), Ct));

        // Mesmo refazendo a parte (pasta já registrada), o arquivo existente fica.
        await MarcarFalhaAsync(tarefa, ParteBranch.Front);
        await WorktreeAsync(service => service.CriarAsync(tarefa, branch, Ct));
        Assert.Equal("ajustado na tarefa", await File.ReadAllTextAsync(Path.Combine(CaminhoFront, ".env.development"), Ct));
    }

    [Fact]
    public async Task RemocaoLimpaAsync()
    {
        var tarefa = await NaEntradaAsync();
        var branch = Implementacao(await IniciarAsync(tarefa, FrontEBack)).Identifier;

        var resultado = await WorktreeAsync(service => service.RemoverAsync(tarefa, branch, Ct));

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        var worktree = resultado.Branch!.Worktree!;
        Assert.Equal(SituacaoWorktree.Removido, worktree.Situacao);
        Assert.All(worktree.Partes, parte => Assert.Equal(SituacaoWorktree.Removido, parte.Situacao));
        Assert.Empty(Git.WorktreesDo(CloneFront));
        Assert.Empty(Git.WorktreesDo(CloneBack));
        Assert.False(Directory.Exists(PastaDaTarefa));

        // Sem worktree ativo: nova remoção é 404.
        Assert.Equal(TarefaResultadoStatus.NaoEncontrada, (await WorktreeAsync(service => service.RemoverAsync(tarefa, branch, Ct))).Status);
    }

    [Fact]
    public async Task AlteracoesNaoCommitadasImpedemARemocaoAsync()
    {
        var tarefa = await NaEntradaAsync();
        var branch = Implementacao(await IniciarAsync(tarefa, FrontEBack)).Identifier;
        Git.Pendencias[CaminhoBack] = new PendenciasDoWorktree(2, 0);

        var resultado = await WorktreeAsync(service => service.RemoverAsync(tarefa, branch, Ct));

        Assert.Equal(TarefaResultadoStatus.WorktreeComPendencias, resultado.Status);
        Assert.Contains("back: 2 alterações não commitadas", resultado.Detalhe, StringComparison.Ordinal);
        Assert.DoesNotContain("front:", resultado.Detalhe, StringComparison.Ordinal);
        Assert.True(Directory.Exists(CaminhoFront));
        Assert.True(Directory.Exists(CaminhoBack));
        Assert.DoesNotContain(Git.Chamadas, chamada => chamada.StartsWith("remover:", StringComparison.Ordinal));
        Assert.Equal(SituacaoWorktree.Criado, (await DetalheAsync(tarefa)).Branches.Single(b => b.Identifier == branch).Worktree!.Situacao);
    }

    [Fact]
    public async Task CommitsNaoEnviadosImpedemARemocaoAsync()
    {
        var tarefa = await NaEntradaAsync();
        var branch = Implementacao(await IniciarAsync(tarefa, FrontEBack)).Identifier;
        Git.Pendencias[CaminhoFront] = new PendenciasDoWorktree(0, 1);

        var resultado = await WorktreeAsync(service => service.RemoverAsync(tarefa, branch, Ct));

        Assert.Equal(TarefaResultadoStatus.WorktreeComPendencias, resultado.Status);
        Assert.Contains("front: 1 commit não enviado", resultado.Detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArquivoEmUsoMantemAsPartesJaRemovidasAsync()
    {
        var tarefa = await NaEntradaAsync();
        var branch = Implementacao(await IniciarAsync(tarefa, FrontEBack)).Identifier;
        Git.FalharEm(CaminhoBack, "remover", new GitComandoRecusadoException($"remover o worktree {CaminhoBack}", "error: failed to delete: Permission denied"));

        var erro = await Assert.ThrowsAsync<DomainException>(() => WorktreeAsync(service => service.RemoverAsync(tarefa, branch, Ct)));

        Assert.Contains("Permission denied", erro.Message, StringComparison.Ordinal);
        Assert.Contains("Feche a IDE e pare os servidores da tarefa", erro.Message, StringComparison.Ordinal);
        var worktree = (await DetalheAsync(tarefa)).Branches.Single(b => b.Identifier == branch).Worktree!;
        Assert.Equal(SituacaoWorktree.Removido, worktree.Partes[0].Situacao);
        Assert.Equal(SituacaoWorktree.Criado, worktree.Partes[1].Situacao);

        // Depois de fechar a IDE, a nova tentativa conclui.
        Git.NaoFalharMais(CaminhoBack, "remover");
        var resultado = await WorktreeAsync(service => service.RemoverAsync(tarefa, branch, Ct));
        Assert.Equal(SituacaoWorktree.Removido, resultado.Branch!.Worktree!.Situacao);
    }

    [Fact]
    public async Task RemocaoComPastaApagadaAMaoLimpaORegistroAsync()
    {
        var tarefa = await NaEntradaAsync();
        var branch = Implementacao(await IniciarAsync(tarefa, FrontEBack)).Identifier;
        Directory.Delete(CaminhoFront, recursive: true);

        var resultado = await WorktreeAsync(service => service.RemoverAsync(tarefa, branch, Ct));

        Assert.Equal(SituacaoWorktree.Removido, resultado.Branch!.Worktree!.Situacao);
        Assert.Contains($"limpar:{CloneFront}", Git.Chamadas);
        Assert.Empty(Git.WorktreesDo(CloneFront));
    }

    [Fact]
    public async Task RecriarDepoisDeRemoverUsaAMesmaPastaAsync()
    {
        var tarefa = await NaEntradaAsync();
        var branch = Implementacao(await IniciarAsync(tarefa, FrontEBack)).Identifier;
        await WorktreeAsync(service => service.RemoverAsync(tarefa, branch, Ct));
        await AlterarEmpresaAsync(empresa => empresa.PastaWorktrees = Path.Combine(_raiz, "outra"));

        var resultado = await WorktreeAsync(service => service.CriarAsync(tarefa, branch, Ct));

        Assert.Equal(PastaDaTarefa, resultado.Branch!.Worktree!.Pasta);
        Assert.Equal(SituacaoWorktree.Criado, resultado.Branch.Worktree.Situacao);
        Assert.True(Directory.Exists(CaminhoFront));
    }

    [Fact]
    public async Task BranchComWorktreeAtivoNaoPodeSerExcluidaAsync()
    {
        var tarefa = await NaEntradaAsync();
        const string Outra = "261113-ipi-front";
        await IniciarAsync(tarefa, FrontEBack);
        NoRemoto(Outra);
        var outra = (await TarefasAsync(service => service.AdicionarBranchAsync(tarefa, new AdicionarBranchRequest(TipoBranch.Implementacao, Outra, [ParteBranch.Front]), Ct))).Branch!;

        var erro = await Assert.ThrowsAsync<DomainException>(() => TarefasAsync(service => service.RemoverBranchAsync(tarefa, outra.Identifier, Ct)));
        Assert.Contains("Remova o worktree da branch antes", erro.Message, StringComparison.Ordinal);
        Assert.Contains((await LerTarefaAsync(Nome))!.Branches, branch => branch.Nome == Outra);

        // Removido o worktree, a branch pode ser excluída (e o registro do worktree vai junto).
        await WorktreeAsync(service => service.RemoverAsync(tarefa, outra.Identifier, Ct));
        Assert.Equal(TarefaResultadoStatus.Sucesso, (await TarefasAsync(service => service.RemoverBranchAsync(tarefa, outra.Identifier, Ct))).Status);
    }

    [Fact]
    public async Task ComandoDoClaudeAsync()
    {
        var tarefa = await NaEntradaAsync();

        var worktree = Implementacao(await IniciarAsync(tarefa, FrontEBack)).Worktree!;

        Assert.Equal($"cd '{PastaDaTarefa}'; claude", worktree.ComandoClaude);
        Assert.Equal(@"cd 'E:\wt\d''avila\x'; claude", WorktreesLocais.ComandoClaude(@"E:\wt\d'avila\x"));
    }

    [Fact]
    public async Task BranchSemWorktreeNoDetalheAsync()
    {
        var tarefa = await NaEntradaAsync(configurarLocal: false);
        await IniciarAsync(tarefa, FrontEBack);

        var detalhe = await DetalheAsync(tarefa);

        Assert.All(detalhe.Branches, branch => Assert.Null(branch.Worktree));
    }

    /// <summary>
    /// Tarefa na entrada, com os repositórios do GitHub e (por padrão) os clones, a pasta dos worktrees e a branch
    /// <see cref="Feature"/> no remoto dos dois clones
    /// </summary>
    private async Task<Guid> NaEntradaAsync(bool configurarLocal = true)
    {
        await ConcluirCargaInicialAsync();
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();
        await DefinirRepositoriosAsync(Workspace, RepositorioFront, RepositorioBack);

        Directory.CreateDirectory(CloneFront);
        Directory.CreateDirectory(CloneBack);
        Git.ComClone(CloneFront, "https://github.com/ixact/erp-web.git", Feature)
            .ComClone(CloneBack, "https://github.com/ixact/erp-api.git", Feature);

        if (configurarLocal)
        {
            await AlterarEmpresaAsync(empresa =>
            {
                empresa.CloneFront = CloneFront;
                empresa.CloneBack = CloneBack;
                empresa.PastaWorktrees = PastaWorktrees;
            });
        }

        return (await LerTarefaAsync(Nome))!.Identifier;
    }

    private async Task<(Guid Tarefa, Guid Branch)> IniciadaComFalhaNoBackAsync()
    {
        var tarefa = await NaEntradaAsync();
        Git.FalharEm(CloneBack, "buscar");
        var branch = Implementacao(await IniciarAsync(tarefa, FrontEBack));
        Assert.Equal(SituacaoWorktree.Falhou, branch.Worktree!.Partes[1].Situacao);
        Git.NaoFalharMais(CloneBack, "buscar");

        return (tarefa, branch.Identifier);
    }

    private void NoRemoto(string branch)
    {
        Git.ComBranchNoRemoto(CloneFront, branch).ComBranchNoRemoto(CloneBack, branch);
    }

    private Task<TarefaResultado> IniciarAsync(Guid tarefa, IReadOnlyList<ParteBranch> partes)
    {
        return TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo, partes), Ct));
    }

    private static BranchResponse Implementacao(TarefaResultado resultado)
    {
        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        return resultado.Tarefa!.Branches.Single(branch => branch.Tipo == TipoBranch.Implementacao && branch.Nome == Feature);
    }

    private async Task AlterarEmpresaAsync(Action<Empresa> alterar)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var empresa = await contexto.Set<Empresa>().SingleAsync(e => e.AsanaWorkspaceGid == Workspace, Ct);
        alterar(empresa);
        await contexto.SaveChangesAsync(Ct);
    }

    private async Task MarcarFalhaAsync(Guid tarefa, ParteBranch parte)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var lida = await TarefaCiclo.CarregarAsync(contexto, tarefa, Ct);
        lida!.Branches.Single(branch => branch.Nome == Feature).Worktree!.Partes.Single(p => p.Parte == parte).MarcarFalha("simulada");
        await contexto.SaveChangesAsync(Ct);
    }

    private async Task<TarefaDetalheResponse> DetalheAsync(Guid tarefa)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var detalhe = await new PainelService(contexto, TimeProvider.System).ObterDetalheAsync(tarefa, Ct);
        return detalhe!;
    }

    private async Task<T> TarefasAsync<T>(Func<ITarefaService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return await acao(new TarefaService(contexto, _gitHub, Worktrees));
    }

    private async Task<T> WorktreeAsync<T>(Func<IWorktreeService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return await acao(new WorktreeService(contexto, new WorktreesLocais(Git, NullLogger<WorktreesLocais>.Instance)));
    }
}
