using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Errors;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Actum.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da capability <c>task-lifecycle</c> e os de <c>rework-tracking</c> desta change, com o Asana simulado.
/// As tarefas são criadas pela sincronização, como no uso real.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class CicloDeVidaTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP")
{
    private const string Nome = "ipi";

    private const string Main = "main";

    private const string Feature = "feature/ipi-devolvido";

    private const string Fix = "fix/ipi-devolvido-pagamento";

    private static readonly DateOnly Prazo = new(2026, 11, 10);

    [Fact]
    public async Task InicioValidoRegistraBranchesSemChamarOAsanaAsync()
    {
        var tarefa = await NaEntradaAsync();
        LimparChamadas();

        var resultado = await TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest($"  {Main} ", Feature, Prazo), Ct));

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.EmAndamento, lida!.Status);
        Assert.Equal(Prazo, lida.PrazoInterno);
        Assert.Equal(
            [(TipoBranch.Base, Main, false), (TipoBranch.Implementacao, Feature, false)],
            lida.Branches.OrderBy(b => b.Tipo).Select(b => (b.Tipo, b.Nome, b.Enviada)));
        Assert.All(lida.Branches, branch => Assert.Null(branch.RetrabalhoId));
        AssertNenhumaChamadaAoAsana();
    }

    [Fact]
    public async Task InicioComBranchesIguaisERejeitadoAsync()
    {
        var tarefa = await NaEntradaAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, $" {Main}", Prazo), Ct)));

        Assert.Equal("A branch base e a branch de implementação devem ser diferentes.", erro.Message);
        Assert.Equal(StatusTarefa.Entrada, (await LerTarefaAsync(Nome))!.Status);
    }

    [Fact]
    public async Task InicioDeTarefaForaDaEntradaCitaOStatusAtualAsync()
    {
        var tarefa = await NaCargaInicialAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo), Ct)));

        Assert.Contains("Status atual: em andamento", erro.Message);
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
    public async Task TarefaDaCargaInicialRecebeAsDuasBranchesAsync()
    {
        var tarefa = await NaCargaInicialAsync();

        await AdicionarBranchAsync(tarefa, TipoBranch.Base, Main);
        var resultado = await AdicionarBranchAsync(tarefa, TipoBranch.Implementacao, Feature);

        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
        Assert.Equal((TipoBranch.Implementacao, Feature, false, (short?)null), (resultado.Branch!.Tipo, resultado.Branch.Nome, resultado.Branch.Enviada, resultado.Branch.RetrabalhoNumero));
        Assert.Equal([Main, Feature], (await LerTarefaAsync(Nome))!.Branches.OrderBy(b => b.Tipo).Select(b => b.Nome));
    }

    [Fact]
    public async Task NovaBranchNoRetrabalhoFicaVinculadaAoRetrabalhoAbertoAsync()
    {
        var tarefa = await EmRetrabalhoAsync();

        var resultado = await AdicionarBranchAsync(tarefa, TipoBranch.Implementacao, Fix);

        Assert.Equal((short?)1, resultado.Branch!.RetrabalhoNumero);
        var lida = await LerTarefaAsync(Nome);
        var retrabalho = Assert.Single(lida!.Retrabalhos);
        Assert.Equal(retrabalho.Id, lida.Branches.Single(b => b.Nome == Fix).RetrabalhoId);
        Assert.Equal([Main, Feature, Fix], lida.Branches.OrderBy(b => b.Id).Select(b => b.Nome));
    }

    [Fact]
    public async Task BranchComNomeRepetidoEConflitoAsync()
    {
        var tarefa = await IniciadaAsync();

        var resultado = await AdicionarBranchAsync(tarefa, TipoBranch.Implementacao, $" {Feature} ");

        Assert.Equal(TarefaResultadoStatus.BranchRepetida, resultado.Status);
        Assert.Equal(2, (await LerTarefaAsync(Nome))!.Branches.Count);
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
        Assert.Equal([Main], previa.Tags);
        AssertNenhumaChamadaAoAsana();
    }

    [Fact]
    public async Task PreviaListaAsTagsPendentesNaOrdemDeEnvioAsync()
    {
        var tarefa = await IniciadaAsync();

        var previa = await ConclusaoAsync(service => service.ObterPreviaAsync(tarefa, Ct));

        Assert.True(previa!.Permitida);
        Assert.Null(previa.Motivo);
        Assert.Equal([Main, Feature], previa.Tags);
    }

    [Fact]
    public async Task PrimeiraConclusaoAdicionaAsTagsEConcluiNoAsanaENoActumAsync()
    {
        var tarefa = await IniciadaAsync();
        LimparChamadas();
        var antes = DateTime.Now;

        var resultado = await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));

        Assert.Equal([$"tag:{Main}", $"addTag:{Main}", $"tag:{Feature}", $"addTag:{Feature}", "concluir"], Asana.Escritas);
        var remota = Asana.Tarefas[Gid(Nome)];
        Assert.True(remota.Concluida);
        Assert.Equal([Main, Feature], remota.Tags.Select(tag => tag.Nome));

        Assert.Equal(StatusTarefa.Concluida, resultado.Tarefa!.Status);
        var lida = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.Concluida, lida!.Status);
        Assert.NotNull(lida.ConcluidaEm);
        Assert.True(lida.ConcluidaEm >= antes.AddMilliseconds(-1));
        Assert.All(lida.Branches, branch => Assert.True(branch.Enviada));
        Assert.Equal(remota.Tags.Select(tag => tag.Gid), lida.Branches.OrderBy(b => b.Tipo).Select(b => b.AsanaTagGid));
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
        await AdicionarBranchAsync(tarefa, TipoBranch.Implementacao, Fix);
        LimparChamadas();

        await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));

        Assert.Equal([$"tag:{Fix}", $"addTag:{Fix}", "concluir"], Asana.Escritas);
        Assert.Equal([Main, Feature, Fix], Asana.Tarefas[Gid(Nome)].Tags.Select(tag => tag.Nome));
    }

    [Fact]
    public async Task TagJaPresenteNaTarefaNaoEAdicionadaDeNovoAsync()
    {
        var tarefa = await IniciadaAsync();
        var tagExistente = new AsanaTag("tag-existente", Main);
        Asana.Tarefas[Gid(Nome)] = Asana.Tarefas[Gid(Nome)] with { Tags = [tagExistente] };
        LimparChamadas();

        await ConclusaoAsync(service => service.ConcluirAsync(tarefa, Ct));

        Assert.Equal([$"tag:{Feature}", $"addTag:{Feature}", "concluir"], Asana.Escritas);
        var main = (await LerTarefaAsync(Nome))!.Branches.Single(b => b.Nome == Main);
        Assert.True(main.Enviada);
        Assert.Equal("tag-existente", main.AsanaTagGid);
    }

    [Fact]
    public async Task FalhaNoMeioMantemOStatusENovaTentativaContinuaDeOndeParouAsync()
    {
        var tarefa = await IniciadaAsync();
        LimparChamadas();
        Asana.FalharNaEscrita = $"addTag:{Feature}";

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

        Assert.Equal([$"tag:{Feature}", $"addTag:{Feature}", "concluir"], Asana.Escritas);
        Assert.Equal([Main, Feature], Asana.Tarefas[Gid(Nome)].Tags.Select(tag => tag.Nome));
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
        Assert.Equal(TarefaResultadoStatus.NaoEncontrada, (await ConclusaoAsync(service => service.ConcluirAsync(identifier, Ct))).Status);
        Assert.Null(await ConclusaoAsync(service => service.ObterPreviaAsync(identifier, Ct)));
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
    /// Tarefa nova depois da carga inicial: status entrada
    /// </summary>
    private async Task<Guid> NaEntradaAsync()
    {
        await ConcluirCargaInicialAsync();
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();

        return (await LerTarefaAsync(Nome))!.Identifier;
    }

    /// <summary>
    /// Tarefa da carga inicial: em andamento, sem branches
    /// </summary>
    private async Task<Guid> NaCargaInicialAsync(DateOnly? prazoAsana = null)
    {
        Abertas(TarefaAsana(Nome, prazo: prazoAsana));
        await SincronizarAsync();

        return (await LerTarefaAsync(Nome))!.Identifier;
    }

    /// <summary>
    /// Tarefa iniciada com <see cref="Main"/> e <see cref="Feature"/>, prazo <see cref="Prazo"/>
    /// </summary>
    private async Task<Guid> IniciadaAsync()
    {
        var tarefa = await NaEntradaAsync();
        await TarefasAsync(service => service.IniciarAsync(tarefa, new IniciarTarefaRequest(Main, Feature, Prazo), Ct));

        return tarefa;
    }

    /// <summary>
    /// Tarefa iniciada, concluída pelo Actum (tags enviadas) e reaberta no Asana por "Maria"
    /// </summary>
    private async Task<Guid> EmRetrabalhoAsync()
    {
        var tarefa = await IniciadaAsync();
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
        return tarefa;
    }

    private Task<TarefaResultado> AdicionarBranchAsync(Guid tarefa, TipoBranch tipo, string nome)
    {
        return TarefasAsync(service => service.AdicionarBranchAsync(tarefa, new AdicionarBranchRequest(tipo, nome), Ct));
    }

    private async Task<T> TarefasAsync<T>(Func<ITarefaService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return await acao(new TarefaService(contexto));
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
    }

    private void AssertNenhumaChamadaAoAsana()
    {
        Assert.Empty(Asana.Escritas);
        Assert.Empty(Asana.TarefasConsultadas);
        Assert.Empty(Asana.StoriesConsultadas);
        Assert.Empty(Asana.WorkspacesListados);
    }
}
