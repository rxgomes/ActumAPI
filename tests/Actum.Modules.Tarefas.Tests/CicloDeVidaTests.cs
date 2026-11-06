using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Errors;
using Actum.Modules.GitHub.Errors;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Actum.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da capability <c>task-lifecycle</c> e os de <c>rework-tracking</c> desta change, com o Asana e o GitHub
/// simulados. As tarefas são criadas pela sincronização, como no uso real.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class CicloDeVidaTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP")
{
    private const string Nome = "ipi";

    private const string Main = "main";

    private const string Feature = "feature/ipi-devolvido";

    private const string Fix = "fix/ipi-devolvido-pagamento";

    private const string NovaImplementacao = "261106-ajuste-ipi";

    private const string Release = "release/2026-11";

    private const string RepositorioFront = "ixact/erp-web";

    private const string RepositorioBack = "ixact/erp-api";

    private const string ShaFront = "sha-front";

    private const string ShaBack = "sha-back";

    private const string TagMain = $"(Front, Back) => {Main}";

    private const string TagFeature = $"(Front, Back) => {Feature}";

    private const string TagFix = $"(Front) => {Fix}";

    private static readonly DateOnly Prazo = new(2026, 11, 10);

    private static readonly IReadOnlyList<ParteBranch> FrontEBack = [ParteBranch.Front, ParteBranch.Back];

    private readonly GitHubClientFalso _gitHub = new GitHubClientFalso()
        .ComBranch(RepositorioFront, Main, ShaFront)
        .ComBranch(RepositorioBack, Main, ShaBack);

    [Fact]
    public async Task InicioValidoCriaABranchNosRepositoriosERegistraAsBranchesSemChamarOAsanaAsync()
    {
        var tarefa = await NaEntradaAsync();
        LimparChamadas();

        var resultado = await TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest($"  {Main} ", Feature, Prazo, [ParteBranch.Back, ParteBranch.Front]), Ct));

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        Assert.Equal(
            [
                $"sha:{RepositorioFront}:{Main}",
                $"sha:{RepositorioBack}:{Main}",
                $"criar:{RepositorioFront}:{Feature}:{ShaFront}",
                $"criar:{RepositorioBack}:{Feature}:{ShaBack}",
            ],
            _gitHub.Chamadas);

        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.EmAndamento, lida!.Status);
        Assert.Equal(Prazo, lida.PrazoInterno);
        Assert.Equal(
            [(TipoBranch.Base, Main, false), (TipoBranch.Implementacao, Feature, false)],
            lida.Branches.OrderBy(b => b.Tipo).Select(b => (b.Tipo, b.Nome, b.Enviada)));
        Assert.All(lida.Branches, branch => Assert.Null(branch.RetrabalhoId));
        Assert.All(lida.Branches, branch => Assert.Equal(FrontEBack, branch.Partes));
        Assert.Equal([TagMain, TagFeature], resultado.Tarefa!.Branches.Select(b => b.Tag));
        Assert.Equal([FrontEBack, FrontEBack], resultado.Tarefa.Branches.Select(b => b.Partes));
        AssertNenhumaChamadaAoAsana();
    }

    [Fact]
    public async Task InicioComUmaParteSoCriaNoRepositorioDelaAsync()
    {
        var tarefa = await NaEntradaAsync();

        var resultado = await TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo, [ParteBranch.Back]), Ct));

        Assert.Equal([$"sha:{RepositorioBack}:{Main}", $"criar:{RepositorioBack}:{Feature}:{ShaBack}"], _gitHub.Chamadas);
        Assert.Equal([$"(Back) => {Main}", $"(Back) => {Feature}"], resultado.Tarefa!.Branches.Select(b => b.Tag));
        Assert.All((await LerTarefaAsync(Nome))!.Branches, branch => Assert.Equal((false, true), (branch.ParteFront, branch.ParteBack)));
    }

    [Fact]
    public async Task InicioAceitaBranchDeImplementacaoJaExistenteNoRepositorioAsync()
    {
        var tarefa = await NaEntradaAsync();
        _gitHub.ComBranch(RepositorioBack, Feature, "sha-antigo");

        var resultado = await TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo, FrontEBack), Ct));

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        Assert.Equal("sha-antigo", _gitHub.Repositorios[RepositorioBack][Feature]);
        Assert.Equal(ShaFront, _gitHub.Repositorios[RepositorioFront][Feature]);
    }

    [Fact]
    public async Task InicioComBaseInexistenteNaoCriaNadaENaoIniciaAsync()
    {
        var tarefa = await NaEntradaAsync();
        _gitHub.ComBranch(RepositorioFront, "release/2026-12", ShaFront);

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest("release/2026-12", Feature, Prazo, FrontEBack), Ct)));

        Assert.Equal($"A branch base 'release/2026-12' não existe no repositório de back ({RepositorioBack}).", erro.Message);
        Assert.DoesNotContain(_gitHub.Chamadas, chamada => chamada.StartsWith("criar:", StringComparison.Ordinal));
        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.Entrada, lida!.Status);
        Assert.Empty(lida.Branches);
    }

    [Fact]
    public async Task InicioComParteSemRepositorioNaoChamaOGitHubAsync()
    {
        var tarefa = await NaEntradaAsync();
        await DefinirRepositoriosAsync(Workspace, null, RepositorioBack);

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo, FrontEBack), Ct)));

        Assert.Equal("A empresa Aureum-ERP não tem repositório de front no GitHub. Cadastre-o na página Empresas.", erro.Message);
        Assert.Empty(_gitHub.Chamadas);
        Assert.Equal(StatusTarefa.Entrada, (await LerTarefaAsync(Nome))!.Status);
    }

    [Fact]
    public async Task FalhaDoGitHubNoSegundoRepositorioNaoIniciaENovaTentativaAceitaAJaCriadaAsync()
    {
        var tarefa = await NaEntradaAsync();
        _gitHub.FalharNaCriacaoEm = RepositorioBack;

        await Assert.ThrowsAsync<GitHubIndisponivelException>(
            () => TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo, FrontEBack), Ct)));

        var aposFalha = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.Entrada, aposFalha!.Status);
        Assert.Empty(aposFalha.Branches);
        Assert.True(_gitHub.Repositorios[RepositorioFront].ContainsKey(Feature));

        _gitHub.FalharNaCriacaoEm = null;
        var resultado = await TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo, FrontEBack), Ct));

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        Assert.True(_gitHub.Repositorios[RepositorioBack].ContainsKey(Feature));
        Assert.Equal(StatusTarefa.EmAndamento, (await LerTarefaAsync(Nome))!.Status);
    }

    [Fact]
    public async Task InicioComBranchesIguaisERejeitadoSemChamarOGitHubAsync()
    {
        var tarefa = await NaEntradaAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, $" {Main}", Prazo, FrontEBack), Ct)));

        Assert.Equal("A branch base e a branch de implementação devem ser diferentes.", erro.Message);
        Assert.Equal(StatusTarefa.Entrada, (await LerTarefaAsync(Nome))!.Status);
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task InicioSemPartesERejeitadoSemChamarOGitHubAsync()
    {
        var tarefa = await NaEntradaAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo, []), Ct)));

        Assert.Equal("Informe se a branch é do front, do back ou dos dois.", erro.Message);
        Assert.Empty(_gitHub.Chamadas);
    }

    [Theory]
    [InlineData("consumidor final nf")]
    [InlineData("feature:nf")]
    [InlineData("feature..nf")]
    [InlineData("feature/nf.lock")]
    [InlineData("-feature")]
    [InlineData("feature/")]
    [InlineData("nf@{1}")]
    public async Task NomeInvalidoNoGitERejeitadoSemChamarOGitHubAsync(string nome)
    {
        var tarefa = await NaEntradaAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, nome, Prazo, FrontEBack), Ct)));

        Assert.StartsWith($"O nome da branch '{nome}' não é válido no Git", erro.Message);
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task InicioDeTarefaForaDaEntradaCitaOStatusAtualAsync()
    {
        var tarefa = await NaCargaInicialAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo, FrontEBack), Ct)));

        Assert.Contains("Status atual: em andamento", erro.Message);
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task BloqueioDuranteRetrabalhoVoltaParaRetrabalhoAsync()
    {
        var tarefa = await EmRetrabalhoAsync();

        var bloqueada = await TarefasAsync(service => service.BloquearAsync(tarefa, Ct));
        Assert.Equal(StatusTarefa.Bloqueada, bloqueada.Tarefa!.Status);
        Assert.Equal(StatusTarefa.Retrabalho, bloqueada.Tarefa.StatusAnterior);

        var desbloqueada = await TarefasAsync(service => service.DesbloquearAsync(tarefa, Ct));
        Assert.Equal(StatusTarefa.Retrabalho, desbloqueada.Tarefa!.Status);
        Assert.Null(desbloqueada.Tarefa.StatusAnterior);
    }

    [Fact]
    public async Task BloqueioEmAndamentoEDesbloqueioDeTarefaNaoBloqueadaAsync()
    {
        var tarefa = await NaCargaInicialAsync();

        await TarefasAsync(service => service.BloquearAsync(tarefa, Ct));
        Assert.Equal(StatusTarefa.EmAndamento, (await TarefasAsync(service => service.DesbloquearAsync(tarefa, Ct))).Tarefa!.Status);

        var erro = await Assert.ThrowsAsync<DomainException>(() => TarefasAsync(service => service.DesbloquearAsync(tarefa, Ct)));
        Assert.Contains("Status atual: em andamento", erro.Message);
    }

    [Fact]
    public async Task AlterarPrazoInternoEPrioridadeNaoMudaOPrazoDoAsanaNemChamaOAsanaAsync()
    {
        var tarefa = await NaCargaInicialAsync(prazoAsana: new DateOnly(2026, 11, 5));
        LimparChamadas();

        var resultado = await TarefasAsync(service => service.AlterarAsync(tarefa, new AlterarTarefaRequest(new DateOnly(2026, 11, 12), 2), Ct));

        Assert.Equal(new DateOnly(2026, 11, 12), resultado.Tarefa!.PrazoInterno);
        Assert.Equal(new DateOnly(2026, 11, 5), resultado.Tarefa.PrazoAsana);
        Assert.Equal((short)2, resultado.Tarefa.Prioridade);
        var lida = await LerTarefaAsync(Nome);
        Assert.Equal((new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 5), (short?)2), (lida!.PrazoInterno, lida.PrazoAsana, lida.Prioridade));
        AssertNenhumaChamadaAoAsana();
    }

    [Fact]
    public async Task PrioridadeForaDaFaixaERejeitadaAsync()
    {
        var tarefa = await NaCargaInicialAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.AlterarAsync(tarefa, new AlterarTarefaRequest(null, 5), Ct)));

        Assert.Equal("A prioridade deve estar entre 1 e 3.", erro.Message);
    }

    [Fact]
    public async Task TarefaArquivadaNaoPodeTerOPrazoAlteradoAsync()
    {
        var tarefa = await NaCargaInicialAsync();
        Abertas();
        Asana.Tarefas.Remove(Gid(Nome));
        await SincronizarAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.AlterarAsync(tarefa, new AlterarTarefaRequest(Prazo, null), Ct)));

        Assert.Equal("Tarefas arquivadas não podem ser alteradas.", erro.Message);
    }

    [Fact]
    public async Task TarefaDaCargaInicialRecebeAsDuasBranchesConferindoABaseECriandoAImplementacaoAsync()
    {
        var tarefa = await NaCargaInicialAsync();

        await AdicionarBranchAsync(tarefa, TipoBranch.Base, Main, [ParteBranch.Back]);
        Assert.Equal([$"sha:{RepositorioBack}:{Main}"], _gitHub.Chamadas);

        var resultado = await AdicionarBranchAsync(tarefa, TipoBranch.Implementacao, Feature, [ParteBranch.Back]);

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        Assert.Equal((TipoBranch.Implementacao, Feature, false, (short?)null), (resultado.Branch!.Tipo, resultado.Branch.Nome, resultado.Branch.Enviada, resultado.Branch.RetrabalhoNumero));
        Assert.Equal($"(Back) => {Feature}", resultado.Branch.Tag);
        Assert.Equal(
            [$"sha:{RepositorioBack}:{Main}", $"sha:{RepositorioBack}:{Main}", $"criar:{RepositorioBack}:{Feature}:{ShaBack}"],
            _gitHub.Chamadas);
        Assert.Equal([Main, Feature], (await LerTarefaAsync(Nome))!.Branches.OrderBy(b => b.Tipo).Select(b => b.Nome));
    }

    [Fact]
    public async Task NovaBranchNoRetrabalhoECriadaAPartirDaBaseEFicaVinculadaAoRetrabalhoAbertoAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        _gitHub.Chamadas.Clear();

        var resultado = await AdicionarBranchAsync(tarefa, TipoBranch.Implementacao, Fix, [ParteBranch.Front]);

        Assert.Equal([$"sha:{RepositorioFront}:{Main}", $"criar:{RepositorioFront}:{Fix}:{ShaFront}"], _gitHub.Chamadas);
        Assert.Equal((short?)1, resultado.Branch!.RetrabalhoNumero);
        Assert.Equal([ParteBranch.Front], resultado.Branch.Partes);
        var lida = await LerTarefaAsync(Nome);
        var retrabalho = Assert.Single(lida!.Retrabalhos);
        Assert.Equal(retrabalho.Id, lida.Branches.Single(b => b.Nome == Fix).RetrabalhoId);
        Assert.Equal([Main, Feature, Fix], lida.Branches.OrderBy(b => b.Id).Select(b => b.Nome));
    }

    [Fact]
    public async Task ImplementacaoSemBaseERejeitadaSemChamarOGitHubAsync()
    {
        var tarefa = await NaCargaInicialAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(() => AdicionarBranchAsync(tarefa, TipoBranch.Implementacao, Feature));

        Assert.Equal("Registre a branch base antes da branch de implementação.", erro.Message);
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task BaseInexistenteNoRegistroNaoERegistradaAsync()
    {
        var tarefa = await NaCargaInicialAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(() => AdicionarBranchAsync(tarefa, TipoBranch.Base, "develop"));

        Assert.Equal($"A branch base 'develop' não existe no repositório de front ({RepositorioFront}).", erro.Message);
        Assert.Empty((await LerTarefaAsync(Nome))!.Branches);
    }

    [Fact]
    public async Task BranchComNomeRepetidoEConflitoSemChamarOGitHubAsync()
    {
        var tarefa = await IniciadaAsync();
        _gitHub.Chamadas.Clear();

        var resultado = await AdicionarBranchAsync(tarefa, TipoBranch.Implementacao, $" {Feature} ");

        Assert.Equal(TarefaResultadoStatus.BranchRepetida, resultado.Status);
        Assert.Equal(2, (await LerTarefaAsync(Nome))!.Branches.Count);
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task BranchNaoEnviadaPodeSerExcluidaEEnviadaNaoAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        var nova = await AdicionarBranchAsync(tarefa, TipoBranch.Implementacao, Fix);

        var removida = await TarefasAsync(service => service.RemoverBranchAsync(tarefa, nova.Branch!.Identifier, Ct));
        Assert.Equal(TarefaResultadoStatus.Sucesso, removida.Status);
        Assert.DoesNotContain((await LerTarefaAsync(Nome))!.Branches, b => b.Nome == Fix);

        var enviada = (await LerTarefaAsync(Nome))!.Branches.Single(b => b.Nome == Main);
        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.RemoverBranchAsync(tarefa, enviada.Identifier, Ct)));
        Assert.Contains("já foi enviada ao Asana", erro.Message);

        var inexistente = await TarefasAsync(service => service.RemoverBranchAsync(tarefa, Guid.CreateVersion7(), Ct));
        Assert.Equal(TarefaResultadoStatus.NaoEncontrada, inexistente.Status);
    }

    [Fact]
    public async Task BranchNaoPodeSerRegistradaNaEntradaAsync()
    {
        var tarefa = await NaEntradaAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(() => AdicionarBranchAsync(tarefa, TipoBranch.Base, Main));

        Assert.Contains("Status atual: entrada", erro.Message);
    }

    [Fact]
    public async Task PreviaSemBranchDeImplementacaoNaoPermiteConcluirAsync()
    {
        var tarefa = await NaCargaInicialAsync();
        await AdicionarBranchAsync(tarefa, TipoBranch.Base, Main);
        LimparChamadas();

        var previa = await ConclusaoAsync(service => service.ObterPreviaAsync(tarefa, Ct));

        Assert.False(previa!.Permitida);
        Assert.Equal("Informe a branch de implementação antes de concluir.", previa.Motivo);
        Assert.Equal([TagMain], previa.Tags);
        AssertNenhumaChamadaAoAsana();
    }

    [Fact]
    public async Task PreviaListaAsTagsPendentesComAsPartesNaOrdemDeEnvioAsync()
    {
        var tarefa = await IniciadaAsync();

        var previa = await ConclusaoAsync(service => service.ObterPreviaAsync(tarefa, Ct));

        Assert.True(previa!.Permitida);
        Assert.Null(previa.Motivo);
        Assert.Equal([TagMain, TagFeature], previa.Tags);
    }

    [Fact]
    public async Task PrimeiraConclusaoAdicionaAsTagsComAsPartesEConcluiNoAsanaENoActumAsync()
    {
        var tarefa = await IniciadaAsync();
        LimparChamadas();
        var antes = DateTime.Now;

        var resultado = await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));

        Assert.Equal([$"tag:{TagMain}", $"addTag:{TagMain}", $"tag:{TagFeature}", $"addTag:{TagFeature}", "concluir"], Asana.Escritas);
        var remota = Asana.Tarefas[Gid(Nome)];
        Assert.True(remota.Concluida);
        Assert.Equal([TagMain, TagFeature], remota.Tags.Select(tag => tag.Nome));

        Assert.Equal(StatusTarefa.Concluida, resultado.Tarefa!.Status);
        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.Concluida, lida!.Status);
        Assert.NotNull(lida.ConcluidaEm);
        Assert.True(lida.ConcluidaEm >= antes.AddMilliseconds(-1));
        Assert.All(lida.Branches, branch => Assert.True(branch.Enviada));
        Assert.Equal(remota.Tags.Select(tag => tag.Gid), lida.Branches.OrderBy(b => b.Tipo).Select(b => b.AsanaTagGid));
    }

    [Fact]
    public async Task BranchesSemPartesUsamONomeComoTagAsync()
    {
        var tarefa = await IniciadaAsync();
        await RemoverPartesAsync();
        LimparChamadas();

        // A resposta das ações (mesmo mapeamento do detalhe) devolve as branches antigas sem partes e com a tag igual ao nome.
        var pausada = await TarefasAsync(service => service.PausarAsync(tarefa, Ct));
        Assert.All(pausada.Tarefa!.Branches, branch => Assert.Empty(branch.Partes));
        Assert.Equal([Main, Feature], pausada.Tarefa.Branches.Select(b => b.Tag));

        var previa = await ConclusaoAsync(service => service.ObterPreviaAsync(tarefa, Ct));
        Assert.Equal([Main, Feature], previa!.Tags);

        await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));
        Assert.Equal([$"tag:{Main}", $"addTag:{Main}", $"tag:{Feature}", $"addTag:{Feature}", "concluir"], Asana.Escritas);
    }

    [Fact]
    public async Task RetrabalhoNaMesmaBranchSoConcluiAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        LimparChamadas();

        await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));

        Assert.Equal(["concluir"], Asana.Escritas);
        Assert.Equal(StatusTarefa.Concluida, (await LerTarefaAsync(Nome))!.Status);
    }

    [Fact]
    public async Task RetrabalhoComBranchNovaSoAdicionaANovaEMantemAsAnterioresAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        await AdicionarBranchAsync(tarefa, TipoBranch.Implementacao, Fix, [ParteBranch.Front]);
        LimparChamadas();

        await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));

        Assert.Equal([$"tag:{TagFix}", $"addTag:{TagFix}", "concluir"], Asana.Escritas);
        Assert.Equal([TagMain, TagFeature, TagFix], Asana.Tarefas[Gid(Nome)].Tags.Select(tag => tag.Nome));
    }

    [Fact]
    public async Task TagJaPresenteNaTarefaNaoEAdicionadaDeNovoAsync()
    {
        var tarefa = await IniciadaAsync();
        var tagExistente = new AsanaTag("tag-existente", TagMain);
        Asana.Tarefas[Gid(Nome)] = Asana.Tarefas[Gid(Nome)] with { Tags = [tagExistente, new AsanaTag("so-o-nome", Feature)] };
        LimparChamadas();

        await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));

        // A comparação é pelo texto completo da tag: uma tag só com o nome da branch não conta.
        Assert.Equal([$"tag:{TagFeature}", $"addTag:{TagFeature}", "concluir"], Asana.Escritas);
        var main = (await LerTarefaAsync(Nome))!.Branches.Single(b => b.Nome == Main);
        Assert.True(main.Enviada);
        Assert.Equal("tag-existente", main.AsanaTagGid);
    }

    [Fact]
    public async Task FalhaNoMeioMantemOStatusENovaTentativaContinuaDeOndeParouAsync()
    {
        var tarefa = await IniciadaAsync();
        LimparChamadas();
        Asana.FalharNaEscrita = $"addTag:{TagFeature}";

        await Assert.ThrowsAsync<AsanaIndisponivelException>(() => ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct)));

        var aposFalha = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.EmAndamento, aposFalha!.Status);
        Assert.Null(aposFalha.ConcluidaEm);
        Assert.True(aposFalha.Branches.Single(b => b.Nome == Main).Enviada);
        Assert.False(aposFalha.Branches.Single(b => b.Nome == Feature).Enviada);
        Assert.False(Asana.Tarefas[Gid(Nome)].Concluida);

        Asana.FalharNaEscrita = null;
        LimparChamadas();
        await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));

        Assert.Equal([$"tag:{TagFeature}", $"addTag:{TagFeature}", "concluir"], Asana.Escritas);
        Assert.Equal([TagMain, TagFeature], Asana.Tarefas[Gid(Nome)].Tags.Select(tag => tag.Nome));
        Assert.Equal(StatusTarefa.Concluida, (await LerTarefaAsync(Nome))!.Status);
    }

    [Fact]
    public async Task ConclusaoSemBranchObrigatoriaERejeitadaSemChamarOAsanaAsync()
    {
        var tarefa = await NaCargaInicialAsync();
        await AdicionarBranchAsync(tarefa, TipoBranch.Base, Main);
        LimparChamadas();

        var erro = await Assert.ThrowsAsync<DomainException>(() => ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct)));

        Assert.Equal("Informe a branch de implementação antes de concluir.", erro.Message);
        AssertNenhumaChamadaAoAsana();
        Assert.Equal(StatusTarefa.EmAndamento, (await LerTarefaAsync(Nome))!.Status);
    }

    [Fact]
    public async Task TarefaExcluidaNoAsanaNaoEConcluidaAsync()
    {
        var tarefa = await IniciadaAsync();
        Asana.Tarefas.Remove(Gid(Nome));
        LimparChamadas();

        var erro = await Assert.ThrowsAsync<DomainException>(() => ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct)));

        Assert.Contains("não existe mais no Asana", erro.Message);
        Assert.Empty(Asana.Escritas);
        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.EmAndamento, lida!.Status);
        Assert.All(lida.Branches, branch => Assert.False(branch.Enviada));
    }

    [Fact]
    public async Task TarefaInexistenteDevolveNaoEncontradaAsync()
    {
        var identifier = Guid.CreateVersion7();

        Assert.Equal(TarefaResultadoStatus.NaoEncontrada, (await TarefasAsync(service => service.BloquearAsync(identifier, Ct))).Status);
        Assert.Equal(TarefaResultadoStatus.NaoEncontrada, (await TarefasAsync(service => service.IniciarAsync(identifier, new IniciarTarefaRequest(Main, Feature, Prazo, FrontEBack), Ct))).Status);
        Assert.Equal(TarefaResultadoStatus.NaoEncontrada, (await ConclusaoAsync(service => service.ConcluirAsync(identifier, Ct))).Status);
        Assert.Null(await ConclusaoAsync(service => service.ObterPreviaAsync(identifier, Ct)));
        Assert.Null(await TarefasAsync(service => service.ListarBasesRecentesAsync(identifier, Ct)));
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task NovoPrazoAposReaberturaResolveAPendenciaAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        var novoPrazo = new DateOnly(2026, 11, 7);

        var resultado = await TarefasAsync(service => service.AlterarAsync(tarefa, new AlterarTarefaRequest(novoPrazo, null), Ct));

        Assert.False(resultado.Tarefa!.PrazoInternoPendente);
        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(novoPrazo, lida!.PrazoInterno);
        Assert.False(lida.PrazoInternoPendente);
        var retrabalho = Assert.Single(lida.Retrabalhos);
        Assert.Equal(Prazo, retrabalho.PrazoInternoAnterior);
        Assert.Equal(novoPrazo, retrabalho.PrazoInternoNovo);
    }

    [Fact]
    public async Task IniciarPelaEntradaDeixaEmAndamentoEPausarERetomarAlternamAsync()
    {
        var tarefa = await IniciadaAsync();
        Assert.True((await LerTarefaAsync(Nome))!.EmAndamento);
        LimparChamadas();

        var pausada = await TarefasAsync(service => service.PausarAsync(tarefa, Ct));
        Assert.False(pausada.Tarefa!.EmAndamento);
        Assert.Equal(StatusTarefa.EmAndamento, pausada.Tarefa.Status);

        var erroAoPausar = await Assert.ThrowsAsync<DomainException>(() => TarefasAsync(service => service.PausarAsync(tarefa, Ct)));
        Assert.Equal("Só é possível pausar uma tarefa em andamento.", erroAoPausar.Message);

        var retomada = await TarefasAsync(service => service.RetomarAsync(tarefa, null, Ct));
        Assert.True(retomada.Tarefa!.EmAndamento);
        Assert.True((await LerTarefaAsync(Nome))!.EmAndamento);

        var erroAoRetomar = await Assert.ThrowsAsync<DomainException>(() => TarefasAsync(service => service.RetomarAsync(tarefa, null, Ct)));
        Assert.Equal("A tarefa já está em andamento.", erroAoRetomar.Message);
        AssertNenhumaChamadaAoAsana();
    }

    [Fact]
    public async Task TarefaDaCargaInicialComecaPausadaAsync()
    {
        var tarefa = await NaCargaInicialAsync();

        var lida = await LerTarefaAsync(Nome);
        Assert.Equal((StatusTarefa.EmAndamento, false), (lida!.Status, lida.EmAndamento));
        Assert.True((await TarefasAsync(service => service.RetomarAsync(tarefa, null, Ct))).Tarefa!.EmAndamento);
    }

    [Fact]
    public async Task TarefaDaEntradaNaoERetomadaSemBranchesEPrazoAsync()
    {
        var tarefa = await NaEntradaAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(() => TarefasAsync(service => service.RetomarAsync(tarefa, null, Ct)));

        Assert.Equal("Para iniciar uma tarefa da entrada, informe as branches e o prazo interno.", erro.Message);
    }

    [Fact]
    public async Task BloquearPausaEDesbloquearMantemPausadaAsync()
    {
        var tarefa = await IniciadaAsync();

        await TarefasAsync(service => service.BloquearAsync(tarefa, Ct));
        Assert.False((await LerTarefaAsync(Nome))!.EmAndamento);

        var erro = await Assert.ThrowsAsync<DomainException>(() => TarefasAsync(service => service.RetomarAsync(tarefa, null, Ct)));
        Assert.Equal("Desbloqueie a tarefa antes de iniciá-la.", erro.Message);

        var desbloqueada = await TarefasAsync(service => service.DesbloquearAsync(tarefa, Ct));
        Assert.Equal((StatusTarefa.EmAndamento, false), (desbloqueada.Tarefa!.Status, desbloqueada.Tarefa.EmAndamento));
    }

    [Fact]
    public async Task ConcluirPausaEORetrabalhoComecaPausadoEPodeSerRetomadoAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        Assert.False((await LerTarefaAsync(Nome))!.EmAndamento);

        var retomada = await TarefasAsync(service => service.RetomarAsync(tarefa, null, Ct));

        Assert.Equal((StatusTarefa.Retrabalho, true), (retomada.Tarefa!.Status, retomada.Tarefa.EmAndamento));
    }

    [Fact]
    public async Task RetomarComNovaBranchNoRetrabalhoReaproveitaABaseECriaAImplementacaoVinculadaAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        LimparChamadas();

        var resultado = await RetomarComBranchAsync(tarefa, Main, NovaImplementacao, [ParteBranch.Back]);

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        Assert.Equal((StatusTarefa.Retrabalho, true), (resultado.Tarefa!.Status, resultado.Tarefa.EmAndamento));
        Assert.Equal([$"sha:{RepositorioBack}:{Main}", $"criar:{RepositorioBack}:{NovaImplementacao}:{ShaBack}"], _gitHub.Chamadas);

        var lida = await LerTarefaAsync(Nome);
        var retrabalho = Assert.Single(lida!.Retrabalhos);
        Assert.Equal([Main, Feature, NovaImplementacao], lida.Branches.OrderBy(b => b.Id).Select(b => b.Nome));
        var nova = lida.Branches.Single(b => b.Nome == NovaImplementacao);
        Assert.Equal((TipoBranch.Implementacao, false, retrabalho.Id), (nova.Tipo, nova.Enviada, nova.RetrabalhoId));
        Assert.Equal([ParteBranch.Back], nova.Partes);
        Assert.Null(lida.Branches.Single(b => b.Nome == Main).RetrabalhoId);
        Assert.False(lida.NovaBranchSugerida);
        AssertNenhumaChamadaAoAsana();
    }

    [Fact]
    public async Task RetomarComBaseNovaRegistraABaseVinculadaAoRetrabalhoAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        _gitHub.ComBranch(RepositorioBack, Release, "sha-release");
        LimparChamadas();

        await RetomarComBranchAsync(tarefa, Release, NovaImplementacao, [ParteBranch.Back]);

        Assert.Equal([$"sha:{RepositorioBack}:{Release}", $"criar:{RepositorioBack}:{NovaImplementacao}:sha-release"], _gitHub.Chamadas);
        var lida = await LerTarefaAsync(Nome);
        var retrabalho = Assert.Single(lida!.Retrabalhos);
        var baseNova = lida.Branches.Single(b => b.Nome == Release);
        Assert.Equal((TipoBranch.Base, retrabalho.Id), (baseNova.Tipo, baseNova.RetrabalhoId));
        Assert.Equal(retrabalho.Id, lida.Branches.Single(b => b.Nome == NovaImplementacao).RetrabalhoId);
    }

    [Fact]
    public async Task RetomarSemCorpoNaoChamaOGitHubNemRegistraBranchesAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        LimparChamadas();

        var retomada = await TarefasAsync(service => service.RetomarAsync(tarefa, null, Ct));

        Assert.True(retomada.Tarefa!.EmAndamento);
        Assert.Empty(_gitHub.Chamadas);
        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(2, lida!.Branches.Count);
        Assert.True(lida.NovaBranchSugerida);
    }

    [Fact]
    public async Task RetomarComBaseInexistenteNaoCriaNemGravaEMantemPausadaAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        LimparChamadas();

        var erro = await Assert.ThrowsAsync<DomainException>(() => RetomarComBranchAsync(tarefa, "develop", NovaImplementacao, FrontEBack));

        Assert.Equal($"A branch base 'develop' não existe no repositório de front ({RepositorioFront}).", erro.Message);
        Assert.DoesNotContain(_gitHub.Chamadas, chamada => chamada.StartsWith("criar:", StringComparison.Ordinal));
        var lida = await LerTarefaAsync(Nome);
        Assert.False(lida!.EmAndamento);
        Assert.Equal(2, lida.Branches.Count);
    }

    [Fact]
    public async Task FalhaDoGitHubAoRetomarNaoGravaEMantemPausadaAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        _gitHub.FalharNaCriacaoEm = RepositorioBack;

        await Assert.ThrowsAsync<GitHubIndisponivelException>(() => RetomarComBranchAsync(tarefa, Main, NovaImplementacao, FrontEBack));

        var lida = await LerTarefaAsync(Nome);
        Assert.False(lida!.EmAndamento);
        Assert.DoesNotContain(lida.Branches, b => b.Nome == NovaImplementacao);
        Assert.True(lida.NovaBranchSugerida);
    }

    [Fact]
    public async Task RetomarComImplementacaoRepetidaEConflitoSemChamarOGitHubAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        LimparChamadas();

        var resultado = await RetomarComBranchAsync(tarefa, Main, $" {Feature} ", FrontEBack);

        Assert.Equal(TarefaResultadoStatus.BranchRepetida, resultado.Status);
        Assert.Empty(_gitHub.Chamadas);
        Assert.False((await LerTarefaAsync(Nome))!.EmAndamento);
    }

    [Fact]
    public async Task RetomarComBaseRegistradaComoImplementacaoERejeitadoSemChamarOGitHubAsync()
    {
        var tarefa = await EmRetrabalhoAsync();
        LimparChamadas();

        var erro = await Assert.ThrowsAsync<DomainException>(() => RetomarComBranchAsync(tarefa, Feature, NovaImplementacao, FrontEBack));

        Assert.Equal($"A branch '{Feature}' já está registrada nesta tarefa como branch de implementação e não pode ser usada como base.", erro.Message);
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task RetomarComCorpoForaDoRetomarTemAMesmaMensagemDoRetomarSemCorpoAsync()
    {
        var naEntrada = await NaEntradaAsync();
        var erroNaEntrada = await Assert.ThrowsAsync<DomainException>(() => RetomarComBranchAsync(naEntrada, Main, NovaImplementacao, FrontEBack));
        Assert.Equal("Para iniciar uma tarefa da entrada, informe as branches e o prazo interno.", erroNaEntrada.Message);

        await TarefasAsync(service => service.IniciarAsync(naEntrada, new IniciarTarefaRequest(Main, Feature, Prazo, FrontEBack), Ct));
        var erroEmAndamento = await Assert.ThrowsAsync<DomainException>(() => RetomarComBranchAsync(naEntrada, Main, NovaImplementacao, FrontEBack));
        Assert.Equal("A tarefa já está em andamento.", erroEmAndamento.Message);

        await TarefasAsync(service => service.BloquearAsync(naEntrada, Ct));
        LimparChamadas();
        var erroBloqueada = await Assert.ThrowsAsync<DomainException>(() => RetomarComBranchAsync(naEntrada, Main, NovaImplementacao, FrontEBack));
        Assert.Equal("Desbloqueie a tarefa antes de iniciá-la.", erroBloqueada.Message);
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task NovaBranchESugeridaSoNoRetrabalhoSemImplementacaoVinculadaAsync()
    {
        var iniciada = await IniciadaAsync();
        Assert.False((await LerTarefaAsync(Nome))!.NovaBranchSugerida);

        await EmRetrabalhoAposConclusaoAsync(iniciada);
        Assert.True((await LerTarefaAsync(Nome))!.NovaBranchSugerida);

        // Uma base vinculada ao retrabalho não basta; a implementação, por "Nova branch", resolve a sugestão.
        _gitHub.ComBranch(RepositorioFront, Release, ShaFront);
        await AdicionarBranchAsync(iniciada, TipoBranch.Base, Release, [ParteBranch.Front]);
        Assert.True((await LerTarefaAsync(Nome))!.NovaBranchSugerida);

        await AdicionarBranchAsync(iniciada, TipoBranch.Implementacao, Fix, [ParteBranch.Front]);
        Assert.False((await LerTarefaAsync(Nome))!.NovaBranchSugerida);
    }

    [Fact]
    public async Task TarefaDesarquivadaIniciadaDeNovoReaproveitaABaseRegistradaAsync()
    {
        var tarefa = await DesarquivadaAsync();
        LimparChamadas();

        var resultado = await TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, NovaImplementacao, Prazo, [ParteBranch.Back]), Ct));

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        Assert.Equal([$"sha:{RepositorioBack}:{Main}", $"criar:{RepositorioBack}:{NovaImplementacao}:{ShaBack}"], _gitHub.Chamadas);
        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.EmAndamento, lida!.Status);
        Assert.Equal([Main, Feature, NovaImplementacao], lida.Branches.OrderBy(b => b.Id).Select(b => b.Nome));
        Assert.Single(lida.Branches, b => b.Nome == Main);
    }

    [Fact]
    public async Task TarefaDesarquivadaIniciadaComImplementacaoJaRegistradaEConflitoAsync()
    {
        var tarefa = await DesarquivadaAsync();
        LimparChamadas();

        var resultado = await TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo, FrontEBack), Ct));

        Assert.Equal(TarefaResultadoStatus.BranchRepetida, resultado.Status);
        Assert.Empty(_gitHub.Chamadas);
        Assert.Equal(StatusTarefa.Entrada, (await LerTarefaAsync(Nome))!.Status);
    }

    [Fact]
    public async Task ConclusaoResolveORetrabalhoAbertoAsync()
    {
        var tarefa = await EmRetrabalhoAsync();

        await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));

        var lida = await LerTarefaAsync(Nome);
        var retrabalho = Assert.Single(lida!.Retrabalhos);
        Assert.Equal(lida.ConcluidaEm, retrabalho.ResolvidoEm);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Tarefa nova depois da carga inicial: status entrada, com os repositórios da empresa definidos
    /// </summary>
    private async Task<Guid> NaEntradaAsync()
    {
        await ConcluirCargaInicialAsync();
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();
        await DefinirRepositoriosAsync(Workspace, RepositorioFront, RepositorioBack);

        return (await LerTarefaAsync(Nome))!.Identifier;
    }

    /// <summary>
    /// Tarefa da carga inicial: em andamento, sem branches, com os repositórios da empresa definidos
    /// </summary>
    private async Task<Guid> NaCargaInicialAsync(DateOnly? prazoAsana = null)
    {
        Abertas(TarefaAsana(Nome, prazo: prazoAsana));
        await SincronizarAsync();
        await DefinirRepositoriosAsync(Workspace, RepositorioFront, RepositorioBack);

        return (await LerTarefaAsync(Nome))!.Identifier;
    }

    /// <summary>
    /// Tarefa iniciada com <see cref="Main"/> e <see cref="Feature"/> no front e no back, prazo <see cref="Prazo"/>
    /// </summary>
    private async Task<Guid> IniciadaAsync()
    {
        var tarefa = await NaEntradaAsync();
        await TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo, FrontEBack), Ct));

        return tarefa;
    }

    /// <summary>
    /// Tarefa iniciada, concluída pelo Actum (tags enviadas) e reaberta no Asana por "Maria"
    /// </summary>
    private async Task<Guid> EmRetrabalhoAsync()
    {
        var tarefa = await IniciadaAsync();
        await EmRetrabalhoAposConclusaoAsync(tarefa);

        return tarefa;
    }

    /// <summary>
    /// Conclui pelo Actum a tarefa iniciada e a reabre no Asana por "Maria"
    /// </summary>
    private async Task EmRetrabalhoAposConclusaoAsync(Guid tarefa)
    {
        await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));

        // Concluída, a tarefa sai da lista de abertas do Asana; depois é reaberta, mantendo as tags.
        Abertas();
        await SincronizarAsync();

        var reabertaEm = DateTime.Now.AddMinutes(1);
        var reaberta = Asana.Tarefas[Gid(Nome)] with { Concluida = false, ModificadaEm = reabertaEm };
        Abertas(reaberta);
        Historico(Nome, Reabertura("r1", "Maria", reabertaEm));
        await SincronizarAsync();

        Assert.Equal(StatusTarefa.Retrabalho, (await LerTarefaAsync(Nome))!.Status);
    }

    /// <summary>
    /// Tarefa iniciada, reatribuída no Asana (arquivada) e devolvida ao usuário: volta para a entrada com as branches
    /// <see cref="Main"/> e <see cref="Feature"/> registradas
    /// </summary>
    private async Task<Guid> DesarquivadaAsync()
    {
        var tarefa = await IniciadaAsync();

        Abertas();
        Asana.Tarefas[Gid(Nome)] = TarefaAsana(Nome, responsavelGid: "outra-pessoa");
        await SincronizarAsync();
        Assert.Equal(StatusTarefa.Arquivada, (await LerTarefaAsync(Nome))!.Status);

        Abertas(TarefaAsana(Nome, modificadaEm: DateTime.Now.AddMinutes(1)));
        await SincronizarAsync();

        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.Entrada, lida!.Status);
        Assert.Equal(2, lida.Branches.Count);
        return tarefa;
    }

    private Task<TarefaResultado> RetomarComBranchAsync(Guid tarefa, string branchBase, string branchImplementacao, IReadOnlyList<ParteBranch> partes)
    {
        return TarefasAsync(service => service.RetomarAsync(tarefa, new RetomarTarefaRequest(branchBase, branchImplementacao, partes), Ct));
    }

    /// <summary>
    /// Deixa as branches da tarefa sem partes, como as registradas antes das partes existirem
    /// </summary>
    private async Task RemoverPartesAsync()
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var tarefa = await TarefaCiclo.CarregarAsync(contexto, (await LerTarefaAsync(Nome))!.Identifier, Ct);
        foreach (var branch in tarefa!.Branches)
        {
            contexto.Entry(branch).Property(b => b.ParteFront).CurrentValue = false;
            contexto.Entry(branch).Property(b => b.ParteBack).CurrentValue = false;
        }

        await contexto.SaveChangesAsync(Ct);
    }

    private Task<TarefaResultado> AdicionarBranchAsync(Guid tarefa, TipoBranch tipo, string nome, IReadOnlyList<ParteBranch>? partes = null)
    {
        return TarefasAsync(service => service.AdicionarBranchAsync(tarefa, new AdicionarBranchRequest(tipo, nome, partes ?? FrontEBack), Ct));
    }

    private async Task<T> TarefasAsync<T>(Func<ITarefaService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return await acao(new TarefaService(contexto, _gitHub));
    }

    private async Task<T> ConclusaoAsync<T>(Func<IConclusaoService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return await acao(new ConclusaoService(contexto, Asana, NullLogger<ConclusaoService>.Instance));
    }

    private void LimparChamadas()
    {
        Asana.Escritas.Clear();
        Asana.TarefasConsultadas.Clear();
        Asana.StoriesConsultadas.Clear();
        Asana.WorkspacesListados.Clear();
        _gitHub.Chamadas.Clear();
    }

    private void AssertNenhumaChamadaAoAsana()
    {
        Assert.Empty(Asana.Escritas);
        Assert.Empty(Asana.TarefasConsultadas);
        Assert.Empty(Asana.StoriesConsultadas);
        Assert.Empty(Asana.WorkspacesListados);
    }
}
