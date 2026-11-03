using Actum.Modules.Claude.Errors;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da capability <c>task-analysis</c>, com as tarefas criadas pela sincronização e a IA simulada por
/// <see cref="AnalisadorDeDemandaFalso"/>.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class AnaliseTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP")
{
    private const string Nome = "analise";

    private static readonly DateTime Agora = new(2026, 11, 3, 15, 30, 0);

    private readonly AnalisadorDeDemandaFalso _ia = new();

    private readonly AnalisesEmAndamento _emAndamento = new();

    private readonly RelogioAjustavel _relogio = new(Agora);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PrimeiraAnaliseGravaVersao1ComItensNaoMarcadosAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("Abater o IPI.", ["Ajustar cálculo", "Criar teste"], ["Vale para notas antigas?"]);

        var resultado = await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct));

        Assert.Equal(AnaliseResultadoStatus.Sucesso, resultado.Status);
        var analise = resultado.Analise!;
        Assert.Equal(1, analise.Versao);
        Assert.True(analise.Atual);
        Assert.Equal("Abater o IPI.", analise.Objetivo);
        Assert.Equal(["Ajustar cálculo", "Criar teste"], analise.Plano().Select(item => item.Texto));
        Assert.Equal([1, 2], analise.Plano().Select(item => (int)item.Ordem));
        Assert.Equal(TipoItemAnalise.Duvida, Assert.Single(analise.Duvidas).Tipo);
        Assert.All(analise.Plano().Concat(analise.Duvidas), item => Assert.False(item.Concluido));
        Assert.Equal(("claude-opus-5-5", 1000, 200), (analise.Modelo, analise.TokensEntrada, analise.TokensSaida));
        Assert.Equal(Agora, analise.AnalisadaEm);
    }

    [Fact]
    public async Task ChecklistAgrupadoComTrechoVagoETestesMarcaveisAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Resultado = new Actum.Modules.Claude.Contracts.DemandaEstruturada(
            "Abater o IPI.",
            [
                new("Front", "Exibir os campos", null),
                new("Back: cálculo", "Somar no item", null),
                new("Back: cálculo", "Somar no pagamento", "Não diz em quais fluxos."),
                new("Back: integração TecnoSpeed", "Preencher ValorIpiDevolvido", null),
            ],
            ["Emitir a NF 2958 de Barra Mansa"],
            ["Quais fluxos de pagamento?"],
            "claude-opus-5-5",
            1000,
            200);

        var analise = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct))).Analise!;

        Assert.Equal(["Front", "Back: cálculo", "Back: integração TecnoSpeed"], analise.Checklist.Select(grupo => grupo.Nome));
        Assert.Equal([1, 2, 1], analise.Checklist.Select(grupo => grupo.Itens.Count));
        Assert.Equal([1, 2, 3, 4], analise.Plano().Select(item => (int)item.Ordem));
        var vago = analise.Checklist[1].Itens[1];
        Assert.Equal(("Somar no pagamento", "Back: cálculo", "Não diz em quais fluxos."), (vago.Texto, vago.Grupo, vago.Vago));
        Assert.All(analise.Plano().Where(item => item != vago), item => Assert.Null(item.Vago));

        var teste = Assert.Single(analise.Testes);
        Assert.Equal((TipoItemAnalise.Teste, "Emitir a NF 2958 de Barra Mansa", (string?)null), (teste.Tipo, teste.Texto, teste.Grupo));
        var marcado = await AnaliseAsync(service => service.MarcarItemAsync(tarefa, teste.Identifier, true, Ct));
        Assert.True(marcado.Item!.Concluido);

        // Na reanálise, o teste com o mesmo texto já vem marcado.
        var segunda = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct))).Analise!;
        Assert.True(Assert.Single(segunda.Testes).Concluido);
    }

    [Fact]
    public async Task ContextoEnviadoTemTodosOsDadosDaTarefaAsync()
    {
        var tarefa = await TarefaComComentariosAsync();

        await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct));

        var demanda = Assert.Single(_ia.Demandas);
        Assert.Equal("Aureum-ERP", demanda.Empresa);
        Assert.Equal($"Tarefa {Nome}", demanda.Titulo);
        Assert.Equal($"notas {Nome}", demanda.Descricao);
        Assert.Equal("Projeto A, Projeto B", demanda.Projetos);
        Assert.Equal(["Primeiro", "Segundo"], demanda.Comentarios.Select(comentario => comentario.Texto));
        Assert.Equal("Ana", demanda.Comentarios[0].Autor);
        Assert.Empty(demanda.Retrabalhos);
    }

    [Fact]
    public async Task ContextoDeTarefaReabertaTrazOsRetrabalhosAsync()
    {
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();
        var conclusao = Base.AddDays(1);
        await AlterarTarefaAsync(Nome, status: StatusTarefa.Concluida, concluidaEm: conclusao);
        Abertas();
        Asana.Tarefas[Gid(Nome)] = TarefaAsana(Nome, concluida: true, modificadaEm: conclusao);
        await SincronizarAsync();
        var reabertaEm = conclusao.AddHours(3);
        Abertas(TarefaAsana(Nome, modificadaEm: reabertaEm));
        Historico(Nome, Reabertura("r1", "Maria", reabertaEm), Comentario("c1", "Maria", "Faltou o desconto", reabertaEm.AddMinutes(1)));
        await SincronizarAsync();
        var tarefa = (await LerTarefaAsync(Nome))!.Identifier;

        await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct));

        var retrabalho = Assert.Single(Assert.Single(_ia.Demandas).Retrabalhos);
        Assert.Equal((1, reabertaEm, "Maria"), ((int)retrabalho.Numero, retrabalho.ReabertaEm, retrabalho.ReabertaPor));
        Assert.Contains(_ia.Demandas[0].Comentarios, comentario => comentario.Texto == "Faltou o desconto");
    }

    [Fact]
    public async Task ReanaliseGravaVersao2EMantemAVersao1Async()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("Primeira", ["A"], []);
        await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct));

        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("Segunda", ["B"], []);
        var segunda = await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct));

        Assert.Equal(2, segunda.Analise!.Versao);
        var lista = (await AnaliseAsync(service => service.ListarAsync(tarefa, Ct)))!;
        Assert.Equal([(2, true, "Segunda"), (1, false, "Primeira")], lista.Select(analise => ((int)analise.Versao, analise.Atual, analise.Objetivo)));
        Assert.Equal("A", Assert.Single(lista[1].Plano()).Texto);
    }

    [Fact]
    public async Task ReanaliseHerdaMarcacoesDeItensIguaisAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("O", ["Ajustar cálculo", "Criar teste"], ["Vale para notas antigas?"]);
        var primeira = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct))).Analise!;
        await AnaliseAsync(service => service.MarcarItemAsync(tarefa, primeira.Plano()[0].Identifier, true, Ct));
        await AnaliseAsync(service => service.MarcarItemAsync(tarefa, primeira.Duvidas[0].Identifier, true, Ct));

        _relogio.Advance(TimeSpan.FromHours(2));
        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("O", ["  AJUSTAR CALCULO ", "Novo item"], ["vale para notas antigas?", "Outra?"]);
        var segunda = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct))).Analise!;

        Assert.Equal([(true, (DateTime?)Agora), (false, null)], segunda.Plano().Select(item => (item.Concluido, item.ConcluidoEm)));
        Assert.Equal([true, false], segunda.Duvidas.Select(item => item.Concluido));
    }

    [Fact]
    public async Task TarefaInexistenteNaoChamaAIaAsync()
    {
        var resultado = await AnaliseAsync(service => service.AnalisarAsync(Guid.NewGuid(), Ct));

        Assert.Equal(AnaliseResultadoStatus.NaoEncontrada, resultado.Status);
        Assert.Null(await AnaliseAsync(service => service.ListarAsync(Guid.NewGuid(), Ct)));
        Assert.Empty(_ia.Demandas);
    }

    [Fact]
    public async Task TarefaArquivadaNaoEAnalisadaAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        await AlterarTarefaAsync(Nome, status: StatusTarefa.Arquivada);

        var erro = await Assert.ThrowsAsync<DomainException>(() => AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct)));

        Assert.Equal("Tarefas arquivadas não podem ser analisadas.", erro.Message);
        Assert.Empty(_ia.Demandas);
    }

    [Fact]
    public async Task SegundaAnaliseSimultaneaRecebeEmAndamentoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Bloqueio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var primeira = AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct));
        await _ia.Iniciada.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var segunda = await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct));
        _ia.Bloqueio.SetResult();

        Assert.Equal(AnaliseResultadoStatus.EmAndamento, segunda.Status);
        Assert.Equal(AnaliseResultadoStatus.Sucesso, (await primeira).Status);
        Assert.Single(_ia.Demandas);

        // Liberada ao terminar: uma nova análise volta a ser aceita.
        _ia.Bloqueio = null;
        Assert.Equal(AnaliseResultadoStatus.Sucesso, (await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct))).Status);
    }

    [Fact]
    public async Task FalhaDaIaNaoGravaVersaoELiberaATarefaAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Falha = new ClaudeIndisponivelException();

        await Assert.ThrowsAsync<ClaudeIndisponivelException>(() => AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct)));

        Assert.Empty((await AnaliseAsync(service => service.ListarAsync(tarefa, Ct)))!);
        _ia.Falha = null;
        Assert.Equal(AnaliseResultadoStatus.Sucesso, (await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct))).Status);
    }

    [Fact]
    public async Task MarcarEDesmarcarItemDaVersaoAtualAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        var item = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct))).Analise!.Plano()[0];

        var marcado = await AnaliseAsync(service => service.MarcarItemAsync(tarefa, item.Identifier, true, Ct));
        Assert.Equal((true, (DateTime?)Agora), (marcado.Item!.Concluido, marcado.Item.ConcluidoEm));

        _relogio.Advance(TimeSpan.FromMinutes(5));
        var remarcado = await AnaliseAsync(service => service.MarcarItemAsync(tarefa, item.Identifier, true, Ct));
        Assert.Equal(Agora, remarcado.Item!.ConcluidoEm);

        var desmarcado = await AnaliseAsync(service => service.MarcarItemAsync(tarefa, item.Identifier, false, Ct));
        Assert.Equal((false, (DateTime?)null), (desmarcado.Item!.Concluido, desmarcado.Item.ConcluidoEm));
        Assert.False((await AnaliseAsync(service => service.ListarAsync(tarefa, Ct)))![0].Plano()[0].Concluido);
    }

    [Fact]
    public async Task ItemDeVersaoAnteriorNaoPodeSerMarcadoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        var antigo = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct))).Analise!.Plano()[0];
        await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct));

        var erro = await Assert.ThrowsAsync<DomainException>(() => AnaliseAsync(service => service.MarcarItemAsync(tarefa, antigo.Identifier, true, Ct)));

        Assert.Equal("Só os itens da análise atual podem ser marcados.", erro.Message);
    }

    [Fact]
    public async Task ItemDeOutraTarefaNaoEEncontradoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        var item = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct))).Analise!.Plano()[0];
        Abertas(TarefaAsana(Nome), TarefaAsana("outra"));
        await SincronizarAsync();
        var outra = (await LerTarefaAsync("outra"))!.Identifier;

        var resultado = await AnaliseAsync(service => service.MarcarItemAsync(outra, item.Identifier, true, Ct));

        Assert.Equal(AnaliseResultadoStatus.NaoEncontrada, resultado.Status);
        Assert.Equal(AnaliseResultadoStatus.NaoEncontrada, (await AnaliseAsync(service => service.MarcarItemAsync(tarefa, Guid.NewGuid(), true, Ct))).Status);
    }

    [Fact]
    public async Task TabelasSaoConsultaveisSemAspasAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        await AnaliseAsync(service => service.AnalisarAsync(tarefa, Ct));

        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var tipos = await contexto.Database
            .SqlQueryRaw<string>("select data_type as \"Value\" from information_schema.columns where table_schema = 'tarefas' and table_name in ('analise', 'analise_item') and column_name in ('analisada_em', 'concluido_em')")
            .ToListAsync(Ct);
        var itens = await contexto.Database
            .SqlQueryRaw<int>("select count(*)::int as \"Value\" from tarefas.analise_item i join tarefas.analise a on a.id = i.analise_id")
            .SingleAsync(Ct);

        Assert.Equal(["timestamp without time zone", "timestamp without time zone"], tipos);
        Assert.True(itens > 0);
    }

    private async Task<Guid> TarefaComComentariosAsync()
    {
        Abertas(TarefaAsana(Nome));
        Historico(Nome, Comentario("c2", "Bruno", "Segundo", Base.AddHours(2)), Comentario("c1", "Ana", "Primeiro", Base.AddHours(1)));
        await SincronizarAsync();

        return (await LerTarefaAsync(Nome))!.Identifier;
    }

    private async Task<T> AnaliseAsync<T>(Func<IAnaliseService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);

        return await acao(new AnaliseService(contexto, _ia, _emAndamento, _relogio));
    }

    /// <summary>
    /// Relógio na hora local informada, que avança sob demanda
    /// </summary>
    private sealed class RelogioAjustavel(DateTime agoraLocal) : TimeProvider
    {
        private DateTime _agora = agoraLocal;

        public void Advance(TimeSpan intervalo) => _agora += intervalo;

        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(_agora, TimeZoneInfo.Local.GetUtcOffset(_agora)).ToUniversalTime();
        }
    }
}

/// <summary>
/// Itens do checklist agrupado, na ordem dos grupos
/// </summary>
internal static class AnaliseResponseExtensions
{
    public static IReadOnlyList<Actum.Modules.Tarefas.Contracts.AnaliseItemResponse> Plano(this Actum.Modules.Tarefas.Contracts.AnaliseResponse analise) =>
        analise.Checklist.SelectMany(grupo => grupo.Itens).ToList();
}
