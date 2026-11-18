using Actum.Modules.Asana.Contracts;
using Actum.Modules.Claude.Application;
using Actum.Modules.Claude.Contracts;
using Actum.Modules.Claude.Errors;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
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

    /// <summary>
    /// Regras reais dos anexos legíveis pela análise, com os limites padrão
    /// </summary>
    internal static readonly ArquivosDaAnalise Arquivos = new(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PrimeiraAnaliseGravaVersao1ComItensNaoMarcadosAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("Abater o IPI.", ["Ajustar cálculo", "Criar teste"], ["Vale para notas antigas?"]);

        var resultado = await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));

        Assert.Equal(AnaliseResultadoStatus.Sucesso, resultado.Status);
        var analise = resultado.Analise!;
        Assert.Equal(1, analise.Versao);
        Assert.True(analise.Atual);
        Assert.Equal("Abater o IPI.", analise.Objetivo);
        Assert.Equal(["Ajustar cálculo", "Criar teste"], analise.Plano().Select(item => item.Texto));
        Assert.Equal([1, 2], analise.Plano().Select(item => (int)item.Ordem));
        Assert.Equal(TipoItemAnalise.Duvida, Assert.Single(analise.Duvidas).Tipo);
        Assert.All(analise.Plano().Concat(analise.Duvidas), item => Assert.False(item.Concluido));
        Assert.Empty(analise.Regras);
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
            [new("Quais fluxos de pagamento?", null, null)],
            [],
            "claude-opus-5-5",
            1000,
            200);

        var analise = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!;

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
        var segunda = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!;
        Assert.True(Assert.Single(segunda.Testes).Concluido);
    }

    [Fact]
    public async Task ContextoEnviadoTemTodosOsDadosDaTarefaAsync()
    {
        var tarefa = await TarefaComComentariosAsync();

        await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));

        var demanda = Assert.Single(_ia.Demandas);
        Assert.Equal("Aureum-ERP", demanda.Empresa);
        Assert.Equal($"Tarefa {Nome}", demanda.Titulo);
        Assert.Equal($"notas {Nome}", demanda.Descricao);
        Assert.Equal("Projeto A, Projeto B", demanda.Projetos);
        Assert.Equal(["Primeiro", "Segundo"], demanda.Comentarios!.Select(comentario => comentario.Texto));
        Assert.Equal("Ana", demanda.Comentarios![0].Autor);
        Assert.Empty(demanda.Retrabalhos!);
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
        ConcluiuNoAsana(Nome, conclusao);
        await SincronizarAsync();
        var reabertaEm = conclusao.AddHours(3);
        Abertas(TarefaAsana(Nome, modificadaEm: reabertaEm));
        Historico(Nome, Reabertura("r1", "Maria", reabertaEm), Comentario("c1", "Maria", "Faltou o desconto", reabertaEm.AddMinutes(1)));
        await SincronizarAsync();
        var tarefa = (await LerTarefaAsync(Nome))!.Identifier;

        await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));

        var retrabalho = Assert.Single(Assert.Single(_ia.Demandas).Retrabalhos!);
        Assert.Equal((1, reabertaEm, "Maria"), ((int)retrabalho.Numero, retrabalho.ReabertaEm, retrabalho.ReabertaPor));
        Assert.Contains(_ia.Demandas[0].Comentarios!, comentario => comentario.Texto == "Faltou o desconto");
    }

    [Fact]
    public async Task ReanaliseGravaVersao2EMantemAVersao1Async()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("Primeira", ["A"], []);
        await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));

        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("Segunda", ["B"], []);
        var segunda = await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));

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
        var primeira = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!;
        await AnaliseAsync(service => service.MarcarItemAsync(tarefa, primeira.Plano()[0].Identifier, true, Ct));
        await AnaliseAsync(service => service.MarcarItemAsync(tarefa, primeira.Duvidas[0].Identifier, true, Ct));

        _relogio.Advance(TimeSpan.FromHours(2));
        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("O", ["  AJUSTAR CALCULO ", "Novo item"], ["vale para notas antigas?", "Outra?"]);
        var segunda = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!;

        Assert.Equal([(true, (DateTime?)Agora), (false, null)], segunda.Plano().Select(item => (item.Concluido, item.ConcluidoEm)));
        Assert.Equal([true, false], segunda.Duvidas.Select(item => item.Concluido));
    }

    [Fact]
    public async Task TarefaInexistenteNaoChamaAIaAsync()
    {
        var resultado = await AnaliseAsync(service => service.AnalisarAsync(Guid.NewGuid(), null, Ct));

        Assert.Equal(AnaliseResultadoStatus.NaoEncontrada, resultado.Status);
        Assert.Null(await AnaliseAsync(service => service.ListarAsync(Guid.NewGuid(), Ct)));
        Assert.Empty(_ia.Demandas);
        Assert.Empty(Asana.StoriesConsultadas);
    }

    [Fact]
    public async Task TarefaArquivadaNaoEAnalisadaAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        await AlterarTarefaAsync(Nome, status: StatusTarefa.Arquivada);
        var consultas = Asana.StoriesConsultadas.Count;

        var erro = await Assert.ThrowsAsync<DomainException>(() => AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct)));

        Assert.Equal("Tarefas arquivadas não podem ser analisadas.", erro.Message);
        Assert.Empty(_ia.Demandas);
        Assert.Equal(consultas, Asana.StoriesConsultadas.Count);
    }

    [Fact]
    public async Task SegundaAnaliseSimultaneaRecebeEmAndamentoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Bloqueio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var primeira = AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));
        await _ia.Iniciada.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var consultas = Asana.StoriesConsultadas.Count;
        var segunda = await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));
        _ia.Bloqueio.SetResult();

        Assert.Equal(AnaliseResultadoStatus.EmAndamento, segunda.Status);
        Assert.Equal(consultas, Asana.StoriesConsultadas.Count);
        Assert.Equal(AnaliseResultadoStatus.Sucesso, (await primeira).Status);
        Assert.Single(_ia.Demandas);

        // Liberada ao terminar: uma nova análise volta a ser aceita.
        _ia.Bloqueio = null;
        Assert.Equal(AnaliseResultadoStatus.Sucesso, (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Status);
    }

    [Fact]
    public async Task FalhaDaIaNaoGravaVersaoELiberaATarefaAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Falha = new ClaudeIndisponivelException();

        await Assert.ThrowsAsync<ClaudeIndisponivelException>(() => AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct)));

        Assert.Empty((await AnaliseAsync(service => service.ListarAsync(tarefa, Ct)))!);
        _ia.Falha = null;
        Assert.Equal(AnaliseResultadoStatus.Sucesso, (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Status);
    }

    [Fact]
    public async Task MarcarEDesmarcarItemDaVersaoAtualAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        var item = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!.Plano()[0];

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
        var antigo = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!.Plano()[0];
        await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));

        var erro = await Assert.ThrowsAsync<DomainException>(() => AnaliseAsync(service => service.MarcarItemAsync(tarefa, antigo.Identifier, true, Ct)));

        Assert.Equal("Só os itens da análise atual podem ser marcados.", erro.Message);
    }

    [Fact]
    public async Task ItemDeOutraTarefaNaoEEncontradoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        var item = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!.Plano()[0];
        Abertas(TarefaAsana(Nome), TarefaAsana("outra"));
        await SincronizarAsync();
        var outra = (await LerTarefaAsync("outra"))!.Identifier;

        var resultado = await AnaliseAsync(service => service.MarcarItemAsync(outra, item.Identifier, true, Ct));

        Assert.Equal(AnaliseResultadoStatus.NaoEncontrada, resultado.Status);
        Assert.Equal(AnaliseResultadoStatus.NaoEncontrada, (await AnaliseAsync(service => service.MarcarItemAsync(tarefa, Guid.NewGuid(), true, Ct))).Status);
    }

    [Fact]
    public async Task ComentarioFeitoNoAsanaDepoisDaSincronizacaoEntraNoContextoComoNaoLidoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        // Comentário novo só no Asana: a tarefa não foi sincronizada de novo.
        Historico(
            Nome,
            Comentario("c2", "Bruno", "Segundo", Base.AddHours(2)),
            Comentario("c1", "Ana", "Primeiro", Base.AddHours(1)),
            Comentario("c3", "Carla", "O desconto não pode passar de 10%", Base.AddHours(3)));

        await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));

        Assert.Equal(["Primeiro", "Segundo", "O desconto não pode passar de 10%"], Assert.Single(_ia.Demandas).Comentarios!.Select(c => c.Texto));
        Assert.Equal([("Primeiro", true), ("Segundo", true), ("O desconto não pode passar de 10%", false)], await ComentariosGravadosAsync());

        // Reanálise sem novidade: nada duplica e os lidos continuam lidos.
        await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));

        Assert.Equal(3, _ia.Demandas[1].Comentarios!.Count);
        Assert.Equal([("Primeiro", true), ("Segundo", true), ("O desconto não pode passar de 10%", false)], await ComentariosGravadosAsync());
    }

    [Fact]
    public async Task FalhaDoAsanaNaoChamaAIaNaoGravaNadaELiberaATarefaAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        Historico(Nome, Comentario("c3", "Carla", "Novo", Base.AddHours(3)));
        Asana.FalharStories = Gid(Nome);

        await Assert.ThrowsAsync<Actum.Modules.Asana.Errors.AsanaIndisponivelException>(() => AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct)));

        Assert.Empty(_ia.Demandas);
        Assert.Empty((await AnaliseAsync(service => service.ListarAsync(tarefa, Ct)))!);
        Assert.Equal(["Primeiro", "Segundo"], (await ComentariosGravadosAsync()).Select(c => c.Texto));

        Asana.FalharStories = null;
        Assert.Equal(AnaliseResultadoStatus.Sucesso, (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Status);
    }

    [Fact]
    public async Task RegrasGravadasEmOrdemComOrigemMarcaveisEHerdadasAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada(
            "O",
            ["Ajustar cálculo"],
            [],
            [new("O desconto não pode passar de 10%", "comentário de Ana em 01/11/2026 11:00"), new("Não alterar o layout da nota", "descrição")]);

        var primeira = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!;

        Assert.Equal(
            [(TipoItemAnalise.Regra, 1, "O desconto não pode passar de 10%", "comentário de Ana em 01/11/2026 11:00"), (TipoItemAnalise.Regra, 2, "Não alterar o layout da nota", "descrição")],
            primeira.Regras.Select(regra => (regra.Tipo, (int)regra.Ordem, regra.Texto, regra.Origem)));
        Assert.All(primeira.Plano().Concat(primeira.Duvidas), item => Assert.Null(item.Origem));

        var marcada = await AnaliseAsync(service => service.MarcarItemAsync(tarefa, primeira.Regras[0].Identifier, true, Ct));
        Assert.Equal((true, (DateTime?)Agora, "comentário de Ana em 01/11/2026 11:00"), (marcada.Item!.Concluido, marcada.Item.ConcluidoEm, marcada.Item.Origem));

        _relogio.Advance(TimeSpan.FromHours(1));
        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("O", ["Ajustar cálculo"], [], [new(" o desconto NÃO pode passar de 10% ", "comentário de Ana em 01/11/2026 11:00")]);
        var segunda = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!;

        Assert.Equal((true, (DateTime?)Agora), (Assert.Single(segunda.Regras).Concluido, segunda.Regras[0].ConcluidoEm));
    }

    [Fact]
    public async Task DuvidasGravadasComOrigemETrechoQueAsGerouAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Resultado = AnalisadorDeDemandaFalso.Estruturada("O", ["Ajustar cálculo"], []) with
        {
            Duvidas =
            [
                new DuvidaDaDemanda("Trocar o cliente repreenche os campos?", "descrição", "Não devem ser preenchidos ao salvar, mas ao criar."),
                new DuvidaDaDemanda("Qual filial?", null, null),
            ],
        };

        var analise = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!;

        Assert.Equal(
            [
                ("Trocar o cliente repreenche os campos?", "descrição", "Não devem ser preenchidos ao salvar, mas ao criar."),
                ("Qual filial?", (string?)null, (string?)null),
            ],
            analise.Duvidas.Select(duvida => (duvida.Texto, duvida.Origem, duvida.Trecho)));
        Assert.All(analise.Plano().Concat(analise.Regras), item => Assert.Null(item.Trecho));
    }

    [Fact]
    public async Task TabelasSaoConsultaveisSemAspasAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));

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

    [Fact]
    public async Task SemCorpoEnviaDescricaoEComentariosEGravaAsFontesPadraoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();

        var analise = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct))).Analise!;

        var demanda = Assert.Single(_ia.Demandas);
        Assert.NotNull(demanda.Descricao);
        Assert.Equal(2, demanda.Comentarios!.Count);
        Assert.Null(demanda.Prompt);
        Assert.Empty(demanda.Arquivos!);
        Assert.Equal((ModoDaAnalise.Tarefa, true, true, (string?)null), (analise.Fontes!.Modo, analise.Fontes.Descricao, analise.Fontes.Comentarios, analise.Fontes.Prompt));
        Assert.Empty(analise.Fontes!.Anexos);
        Assert.Empty(Asana.Baixados);
    }

    [Fact]
    public async Task SoAnexosEnviaOsArquivosSemDescricaoNemComentariosEGravaAsFontesAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        AnexosNoAsana(("a1", "especificacao.pdf", 3L, "asana"), ("a2", "tela.png", 2L, "asana"));
        var fontes = new FontesDaAnaliseRequest(ModoDaAnalise.Tarefa, false, false, ["a2", "a1"], null);

        var analise = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, fontes, Ct))).Analise!;

        var demanda = Assert.Single(_ia.Demandas);
        Assert.Null(demanda.Descricao);
        Assert.Null(demanda.Comentarios);
        Assert.Null(demanda.Retrabalhos);
        Assert.Equal(["tela.png", "especificacao.pdf"], demanda.Arquivos!.Select(arquivo => arquivo.Nome));
        Assert.Equal(["a2", "a1"], Asana.Baixados);
        Assert.Equal((ModoDaAnalise.Tarefa, false, false), (analise.Fontes!.Modo, analise.Fontes.Descricao, analise.Fontes.Comentarios));
        Assert.Equal([new AnexoDaFonteResponse("a2", "tela.png"), new AnexoDaFonteResponse("a1", "especificacao.pdf")], analise.Fontes.Anexos);

        var lida = (await AnaliseAsync(service => service.ListarAsync(tarefa, Ct)))![0];
        Assert.Equal(analise.Fontes.Anexos, lida.Fontes!.Anexos);
    }

    [Fact]
    public async Task ModoPromptEnviaOPromptEOsArquivosCitadosSemADescricaoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        AnexosNoAsana(("a1", "modelo.pdf", 3L, "asana"));
        var fontes = new FontesDaAnaliseRequest(ModoDaAnalise.Prompt, true, true, ["a1"], "  Siga o padrão de @modelo.pdf  ");

        var analise = (await AnaliseAsync(service => service.AnalisarAsync(tarefa, fontes, Ct))).Analise!;

        var demanda = Assert.Single(_ia.Demandas);
        Assert.Equal("Siga o padrão de @modelo.pdf", demanda.Prompt);
        Assert.Null(demanda.Descricao);
        Assert.Equal(2, demanda.Comentarios!.Count);
        Assert.Equal(["modelo.pdf"], demanda.Arquivos!.Select(arquivo => arquivo.Nome));
        Assert.Equal((ModoDaAnalise.Prompt, false, true, "Siga o padrão de @modelo.pdf"), (analise.Fontes!.Modo, analise.Fontes.Descricao, analise.Fontes.Comentarios, analise.Fontes.Prompt));
    }

    [Fact]
    public async Task ComentariosForaDoContextoContinuamSendoAtualizadosDoAsanaAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        Historico(Nome, Comentario("c3", "Carla", "Novo no Asana", Base.AddHours(3)));
        AnexosNoAsana(("a1", "especificacao.pdf", 3L, "asana"));
        var fontes = new FontesDaAnaliseRequest(ModoDaAnalise.Tarefa, true, false, [], null);

        await AnaliseAsync(service => service.AnalisarAsync(tarefa, fontes, Ct));

        Assert.Null(Assert.Single(_ia.Demandas).Comentarios);
        Assert.Contains(("Novo no Asana", false), await ComentariosGravadosAsync());
    }

    [Fact]
    public async Task ChecklistGravaOrigemETrechoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        _ia.Resultado = new DemandaEstruturada(
            "Agrupar.",
            [new("Back", "Agrupar por filial", null, "anexo especificacao.pdf, página 3", "O relatório deve ser agrupado por filial")],
            [],
            [],
            [],
            "claude-opus-5-5",
            10,
            5);

        await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));

        var item = (await AnaliseAsync(service => service.ListarAsync(tarefa, Ct)))![0].Plano().Single();
        Assert.Equal(("anexo especificacao.pdf, página 3", "O relatório deve ser agrupado por filial"), (item.Origem, item.Trecho));
    }

    [Fact]
    public async Task VersaoAnteriorAsFontesVemSemFontesAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        await AnaliseAsync(service => service.AnalisarAsync(tarefa, null, Ct));
        await using (var contexto = TarefasDbContext.Criar(Postgres.DataSource))
        {
            await contexto.Database.ExecuteSqlRawAsync("update tarefas.analise set fonte_modo = null, fonte_descricao = null, fonte_comentarios = null", Ct);
        }

        var lida = (await AnaliseAsync(service => service.ListarAsync(tarefa, Ct)))![0];

        Assert.Null(lida.Fontes);
    }

    public static TheoryData<FontesDaAnaliseRequest, string, string> FontesInvalidas => new()
    {
        { new FontesDaAnaliseRequest(ModoDaAnalise.Tarefa, false, false, [], null), "Fontes", "Escolha ao menos uma fonte para a análise." },
        { new FontesDaAnaliseRequest(ModoDaAnalise.Prompt, null, null, [], "   "), "Prompt", "Escreva o prompt da análise." },
        { new FontesDaAnaliseRequest(ModoDaAnalise.Tarefa, true, true, ["a1", "a1"], null), "Anexos", "O mesmo anexo foi escolhido mais de uma vez." },
        { new FontesDaAnaliseRequest(ModoDaAnalise.Tarefa, true, true, ["outro"], null), "Anexos", "O anexo não está mais na tarefa no Asana." },
        { new FontesDaAnaliseRequest(ModoDaAnalise.Tarefa, true, true, ["a2"], null), "Anexos", "video.mp4: Tipo de arquivo não suportado pela análise." },
        { new FontesDaAnaliseRequest(ModoDaAnalise.Tarefa, true, true, ["a3"], null), "Anexos", "planilha: Arquivos externos não podem ser lidos pela análise." },
        { new FontesDaAnaliseRequest(ModoDaAnalise.Tarefa, true, true, ["a4", "a5"], null), "Anexos", "Os anexos escolhidos somam mais de 20 MB. Escolha menos arquivos." },
    };

    [Theory]
    [MemberData(nameof(FontesInvalidas))]
    public async Task FontesInvalidasNaoChamamAIaNemGravamVersaoAsync(FontesDaAnaliseRequest fontes, string campo, string mensagem)
    {
        var tarefa = await TarefaComComentariosAsync();
        AnexosNoAsana(
            ("a1", "especificacao.pdf", 3L, "asana"),
            ("a2", "video.mp4", 3L, "asana"),
            ("a3", "planilha", null, "gdrive"),
            ("a4", "manual-1.pdf", 15L * 1024 * 1024, "asana"),
            ("a5", "manual-2.pdf", 15L * 1024 * 1024, "asana"));

        var resultado = await AnaliseAsync(service => service.AnalisarAsync(tarefa, fontes, Ct));

        Assert.Equal(AnaliseResultadoStatus.FontesInvalidas, resultado.Status);
        Assert.Equal([mensagem], resultado.Erros![campo]);
        Assert.Empty(_ia.Demandas);
        Assert.Empty(Asana.Baixados);
        Assert.Empty((await AnaliseAsync(service => service.ListarAsync(tarefa, Ct)))!);
    }

    [Fact]
    public async Task FalhaDoAsanaAoBaixarNaoChamaAIaNemGravaVersaoAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        AnexosNoAsana(("a1", "especificacao.pdf", 3L, "asana"));
        Asana.FalhaAoBaixar = new Actum.Modules.Asana.Errors.AsanaIndisponivelException();
        var fontes = new FontesDaAnaliseRequest(ModoDaAnalise.Tarefa, true, true, ["a1"], null);

        await Assert.ThrowsAsync<Actum.Modules.Asana.Errors.AsanaIndisponivelException>(() => AnaliseAsync(service => service.AnalisarAsync(tarefa, fontes, Ct)));

        Assert.Empty(_ia.Demandas);
        Assert.Empty((await AnaliseAsync(service => service.ListarAsync(tarefa, Ct)))!);
    }

    [Fact]
    public async Task AnexoDeOutraTarefaNoDownloadEFonteInvalidaAsync()
    {
        var tarefa = await TarefaComComentariosAsync();
        AnexosNoAsana(("a1", "especificacao.pdf", 3L, "asana"));
        Asana.Conteudos["a1"] = new AsanaConteudoDoAnexo("especificacao.pdf", "outra-tarefa", [1, 2, 3]);
        var fontes = new FontesDaAnaliseRequest(ModoDaAnalise.Tarefa, true, true, ["a1"], null);

        var resultado = await AnaliseAsync(service => service.AnalisarAsync(tarefa, fontes, Ct));

        Assert.Equal(AnaliseResultadoStatus.FontesInvalidas, resultado.Status);
        Assert.Equal(["O anexo não está mais na tarefa no Asana."], resultado.Erros!["Anexos"]);
        Assert.Empty(_ia.Demandas);
    }

    /// <summary>
    /// Anexos da tarefa do cenário no Asana, com conteúdo de 3 bytes para download
    /// </summary>
    private void AnexosNoAsana(params (string Gid, string Nome, long? Tamanho, string Hospedagem)[] anexos)
    {
        var gid = Gid(Nome);
        Asana.AnexosDaTarefa[gid] = anexos.Select(anexo => new AsanaAnexoDaTarefa(anexo.Gid, anexo.Nome, Base, anexo.Tamanho, anexo.Hospedagem)).ToList();
        foreach (var anexo in anexos)
        {
            Asana.Conteudos[anexo.Gid] = new AsanaConteudoDoAnexo(anexo.Nome, gid, [1, 2, 3]);
        }
    }

    private async Task<Guid> TarefaComComentariosAsync()
    {
        Abertas(TarefaAsana(Nome));
        Historico(Nome, Comentario("c2", "Bruno", "Segundo", Base.AddHours(2)), Comentario("c1", "Ana", "Primeiro", Base.AddHours(1)));
        await SincronizarAsync();

        return (await LerTarefaAsync(Nome))!.Identifier;
    }

    /// <summary>
    /// Comentários gravados da tarefa do cenário, do mais antigo para o mais recente, com o indicador de lido
    /// </summary>
    private async Task<List<(string Texto, bool Lido)>> ComentariosGravadosAsync()
    {
        var gid = Gid(Nome);
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var tarefa = await contexto.Set<Tarefa>()
            .AsNoTracking()
            .Include(t => t.Comentarios)
            .SingleAsync(t => t.AsanaGid == gid, Ct);

        return tarefa.Comentarios.OrderBy(c => c.CriadoEm).Select(c => (c.Texto, c.Lido)).ToList();
    }

    private async Task<T> AnaliseAsync<T>(Func<IAnaliseService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);

        return await acao(new AnaliseService(contexto, _ia, Asana, Arquivos, _emAndamento, _relogio));
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
