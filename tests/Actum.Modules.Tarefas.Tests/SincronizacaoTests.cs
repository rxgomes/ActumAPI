using Actum.Modules.Asana.Errors;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Tests.Shared;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da capability <c>task-sync</c>.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class SincronizacaoTests(PostgresFixture postgres)
    : SincronizacaoTestBase(postgres, "Aureum-ERP", "aremas.com.br")
{
    private static readonly DateOnly Ontem = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));

    [Fact]
    public async Task PrimeiroUsoCriaUmaEmpresaPorWorkspaceComCoresDiferentesAsync()
    {
        await ConcluirCargaInicialAsync();

        var aureum = await LerEmpresaAsync(Workspaces[0].Gid);
        var aremas = await LerEmpresaAsync(Workspaces[1].Gid);

        Assert.Equal("Aureum-ERP", aureum.Nome);
        Assert.Equal("aremas.com.br", aremas.Nome);
        Assert.NotEqual(aureum.Cor, aremas.Cor);
        Assert.Matches("^#[0-9A-F]{6}$", aureum.Cor);
        Assert.True(aureum.Sincronizar);
        Assert.True(aremas.Sincronizar);
    }

    [Fact]
    public async Task EmpresaDesligadaNaoEConsultadaEAjusteApareceNaListagemAsync()
    {
        await ConcluirCargaInicialAsync();
        var aremas = await LerEmpresaAsync(Workspaces[1].Gid);

        await using (var contexto = TarefasDbContext.Criar(Postgres.DataSource))
        {
            var service = new EmpresaService(contexto);
            var alterada = await service.AlterarAsync(aremas.Identifier, new AlterarEmpresaRequest("#abcdef", false), TestContext.Current.CancellationToken);
            Assert.NotNull(alterada);

            var listada = Assert.Single(await service.ListarAsync(TestContext.Current.CancellationToken), e => e.Identifier == aremas.Identifier);
            Assert.Equal("#ABCDEF", listada.Cor);
            Assert.False(listada.Sincronizar);
        }

        Asana.WorkspacesListados.Clear();
        await SincronizarAsync();

        Assert.Equal([Workspaces[0].Gid], Asana.WorkspacesListados);
    }

    [Fact]
    public async Task AjusteDeEmpresaInexistenteDevolveNuloAsync()
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);

        var resultado = await new EmpresaService(contexto).AlterarAsync(Guid.CreateVersion7(), new AlterarEmpresaRequest(null, true), TestContext.Current.CancellationToken);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task CargaInicialCriaEmAndamentoComPrazoInternoIgualAoDoAsanaAsync()
    {
        Abertas(TarefaAsana("vencida", prazo: Ontem), TarefaAsana("sem-prazo"));

        await SincronizarAsync();

        var vencida = await LerTarefaAsync("vencida");
        Assert.Equal(StatusTarefa.EmAndamento, vencida!.Status);
        Assert.Equal(Ontem, vencida.PrazoInterno);
        Assert.Equal(Ontem, vencida.PrazoAsana);

        var semPrazo = await LerTarefaAsync("sem-prazo");
        Assert.Equal(StatusTarefa.EmAndamento, semPrazo!.Status);
        Assert.Null(semPrazo.PrazoInterno);
    }

    [Fact]
    public async Task CargaInicialImportaComentariosComoLidosAsync()
    {
        Abertas(TarefaAsana("com-comentarios"));
        Historico(
            "com-comentarios",
            Comentario("c1", "Maria", "Pode priorizar?", Base.AddDays(-2)),
            Reabertura("r-antiga", "Maria", Base.AddDays(-3)));

        await SincronizarAsync();

        var tarefa = await LerTarefaAsync("com-comentarios");
        var comentario = Assert.Single(tarefa!.Comentarios);
        Assert.Equal("Maria", comentario.Autor);
        Assert.Equal("Pode priorizar?", comentario.Texto);
        Assert.Equal(Base.AddDays(-2), comentario.CriadoEm);
        Assert.True(comentario.Lido);
        Assert.Empty(tarefa.Retrabalhos);
    }

    [Fact]
    public async Task CargaInicialSoAconteceUmaVezAsync()
    {
        Abertas(TarefaAsana("primeira"));
        await SincronizarAsync();
        Assert.NotNull((await LerEmpresaAsync(Workspace)).CargaInicialEm);

        Abertas(TarefaAsana("primeira"), TarefaAsana("depois", prazo: Ontem));
        await SincronizarAsync();

        var depois = await LerTarefaAsync("depois");
        Assert.Equal(StatusTarefa.Entrada, depois!.Status);
    }

    [Fact]
    public async Task TarefaNovaAposCargaInicialEntraNaCaixaDeEntradaAsync()
    {
        await ConcluirCargaInicialAsync();
        var prazo = new DateOnly(2026, 12, 10);
        Abertas(TarefaAsana("nova", prazo: prazo));
        Historico("nova", Comentario("c1", "Maria", "Segue a demanda", Base));

        var resultado = await SincronizarAsync();

        var nova = await LerTarefaAsync("nova");
        Assert.Equal(StatusTarefa.Entrada, nova!.Status);
        Assert.Null(nova.PrazoInterno);
        Assert.Equal(prazo, nova.PrazoAsana);
        Assert.Equal("Tarefa nova", nova.Titulo);
        Assert.Equal("Projeto A, Projeto B", nova.Projetos);
        Assert.True(Assert.Single(nova.Comentarios).Lido);
        Assert.Equal(1, resultado.Sincronizacao!.TarefasNovas);
    }

    [Fact]
    public async Task TarefaConcluidaAntesDoActumEReabertaEntraSemRetrabalhoAsync()
    {
        await ConcluirCargaInicialAsync();
        Abertas(TarefaAsana("antiga"));
        Historico("antiga", Reabertura("r1", "Maria", Base));

        await SincronizarAsync();

        var antiga = await LerTarefaAsync("antiga");
        Assert.Equal(StatusTarefa.Entrada, antiga!.Status);
        Assert.Empty(antiga.Retrabalhos);
    }

    [Fact]
    public async Task TarefaDeOutroResponsavelNaoECriadaAsync()
    {
        Abertas(TarefaAsana("minha"), TarefaAsana("de-outro", responsavelGid: "outra-pessoa"));

        await SincronizarAsync();

        Assert.NotNull(await LerTarefaAsync("minha"));
        Assert.Null(await LerTarefaAsync("de-outro"));
    }

    [Fact]
    public async Task PrazoAlteradoNoAsanaNaoMudaCamposInternosAsync()
    {
        await ConcluirCargaInicialAsync();
        Abertas(TarefaAsana("prazo", prazo: new DateOnly(2026, 11, 10)));
        await SincronizarAsync();
        await AlterarTarefaAsync("prazo", status: StatusTarefa.EmAndamento, prazoInterno: new DateOnly(2026, 11, 8));

        Abertas(TarefaAsana("prazo", prazo: new DateOnly(2026, 11, 20), modificadaEm: Base.AddHours(1)));
        var resultado = await SincronizarAsync();

        var tarefa = await LerTarefaAsync("prazo");
        Assert.Equal(new DateOnly(2026, 11, 20), tarefa!.PrazoAsana);
        Assert.Equal(new DateOnly(2026, 11, 8), tarefa.PrazoInterno);
        Assert.Equal(StatusTarefa.EmAndamento, tarefa.Status);
        Assert.Equal(Base.AddHours(1), tarefa.AsanaModificadaEm);
        Assert.Equal(1, resultado.Sincronizacao!.TarefasAtualizadas);
    }

    [Fact]
    public async Task SincronizacaoRepetidaNaoDuplicaNadaAsync()
    {
        Abertas(TarefaAsana("a"), TarefaAsana("b"));
        Historico("a", Comentario("c1", "Maria", "oi", Base));
        await SincronizarAsync();
        Asana.StoriesConsultadas.Clear();

        var segunda = await SincronizarAsync();
        var terceira = await SincronizarAsync();

        Assert.Single((await LerTarefaAsync("a"))!.Comentarios);
        Assert.Empty(Asana.StoriesConsultadas);
        foreach (var resumo in new[] { segunda.Sincronizacao!, terceira.Sincronizacao! })
        {
            Assert.True(resumo.Sucesso);
            Assert.Equal((0, 0, 0, 0), (resumo.TarefasNovas, resumo.TarefasAtualizadas, resumo.TarefasArquivadas, resumo.Reaberturas));
        }

        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var empresa = await LerEmpresaAsync(Workspace);
        Assert.Equal(2, contexto.Set<Tarefa>().Count(tarefa => tarefa.EmpresaId == empresa.Id));
    }

    [Fact]
    public async Task ComentarioNovoEmTarefaModificadaEGravadoComoNaoLidoAsync()
    {
        Abertas(TarefaAsana("comentada"));
        Historico("comentada", Comentario("c1", "Maria", "antigo", Base.AddDays(-1)));
        await SincronizarAsync();

        Abertas(TarefaAsana("comentada", modificadaEm: Base.AddHours(2)));
        Historico(
            "comentada",
            Comentario("c1", "Maria", "antigo", Base.AddDays(-1)),
            Comentario("c2", "João", "Faltou o IPI devolvido", Base.AddHours(2)));
        await SincronizarAsync();

        var comentarios = (await LerTarefaAsync("comentada"))!.Comentarios.OrderBy(c => c.CriadoEm).ToList();
        Assert.Equal(2, comentarios.Count);
        Assert.True(comentarios[0].Lido);
        Assert.Equal(("João", "Faltou o IPI devolvido", Base.AddHours(2), false), (comentarios[1].Autor, comentarios[1].Texto, comentarios[1].CriadoEm, comentarios[1].Lido));
    }

    [Fact]
    public async Task TarefaConcluidaNoAsanaEArquivadaAsync()
    {
        Abertas(TarefaAsana("feita-por-outro"));
        await SincronizarAsync();

        Abertas();
        Asana.Tarefas[Gid("feita-por-outro")] = TarefaAsana("feita-por-outro", concluida: true);
        var resultado = await SincronizarAsync();

        var tarefa = await LerTarefaAsync("feita-por-outro");
        Assert.Equal(StatusTarefa.Arquivada, tarefa!.Status);
        Assert.Equal(MotivoArquivamento.ConcluidaNoAsana, tarefa.MotivoArquivamento);
        Assert.Equal(StatusTarefa.EmAndamento, tarefa.StatusAnterior);
        Assert.NotNull(tarefa.ArquivadaEm);
        Assert.Equal(1, resultado.Sincronizacao!.TarefasArquivadas);
    }

    [Fact]
    public async Task TarefaReatribuidaEArquivadaAsync()
    {
        await ConcluirCargaInicialAsync();
        Abertas(TarefaAsana("reatribuida"));
        await SincronizarAsync();

        Abertas();
        Asana.Tarefas[Gid("reatribuida")] = TarefaAsana("reatribuida", responsavelGid: "outra-pessoa");
        await SincronizarAsync();

        var tarefa = await LerTarefaAsync("reatribuida");
        Assert.Equal(StatusTarefa.Arquivada, tarefa!.Status);
        Assert.Equal(MotivoArquivamento.Reatribuida, tarefa.MotivoArquivamento);
    }

    [Fact]
    public async Task TarefaExcluidaEArquivadaAsync()
    {
        Abertas(TarefaAsana("excluida"));
        await SincronizarAsync();

        Abertas();
        Asana.Tarefas.Remove(Gid("excluida"));
        await SincronizarAsync();

        var tarefa = await LerTarefaAsync("excluida");
        Assert.Equal(StatusTarefa.Arquivada, tarefa!.Status);
        Assert.Equal(MotivoArquivamento.Excluida, tarefa.MotivoArquivamento);
    }

    [Fact]
    public async Task TarefaConcluidaAguardandoTesteNaoEConsultadaAsync()
    {
        Abertas(TarefaAsana("aguardando-teste"));
        await SincronizarAsync();
        await AlterarTarefaAsync("aguardando-teste", status: StatusTarefa.Concluida, concluidaEm: Base.AddDays(1));

        Abertas();
        Asana.TarefasConsultadas.Clear();
        await SincronizarAsync();

        Assert.Equal(StatusTarefa.Concluida, (await LerTarefaAsync("aguardando-teste"))!.Status);
        Assert.Empty(Asana.TarefasConsultadas);
    }

    [Fact]
    public async Task TarefaArquivadaComoReatribuidaQueVoltaVaiParaEntradaAsync()
    {
        Abertas(TarefaAsana("volta"));
        await SincronizarAsync();
        Abertas();
        Asana.Tarefas[Gid("volta")] = TarefaAsana("volta", responsavelGid: "outra-pessoa");
        await SincronizarAsync();

        Abertas(TarefaAsana("volta", modificadaEm: Base.AddDays(1)));
        var resultado = await SincronizarAsync();

        var tarefa = await LerTarefaAsync("volta");
        Assert.Equal(StatusTarefa.Entrada, tarefa!.Status);
        Assert.Null(tarefa.MotivoArquivamento);
        Assert.Null(tarefa.ArquivadaEm);
        Assert.Empty(tarefa.Retrabalhos);
        Assert.Equal(1, resultado.Sincronizacao!.TarefasAtualizadas);
    }

    [Fact]
    public async Task SincronizacaoBemSucedidaERegistradaComTotaisAsync()
    {
        Abertas(Workspaces[0].Gid, TarefaAsana("a"), TarefaAsana("b"));
        Abertas(Workspaces[1].Gid, TarefaAsana("c"));

        var resultado = await SincronizarAsync();

        Assert.Equal(SincronizacaoResultadoStatus.Concluida, resultado.Status);
        var resumo = resultado.Sincronizacao!;
        Assert.True(resumo.Sucesso);
        Assert.Null(resumo.Erro);
        Assert.Equal(3, resumo.TarefasNovas);
        Assert.NotNull(resumo.FinalizadaEm);

        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var ultima = await CriarServico(contexto).ObterUltimaAsync(TestContext.Current.CancellationToken);

        // O banco guarda microssegundos; o resumo devolvido traz a hora em memória (ticks de 100 ns).
        Assert.NotNull(ultima);
        Assert.Equal(resumo with { IniciadaEm = ultima.IniciadaEm, FinalizadaEm = ultima.FinalizadaEm }, ultima);
        Assert.Equal(resumo.IniciadaEm, ultima.IniciadaEm, TimeSpan.FromMicroseconds(1));
        Assert.Equal(resumo.FinalizadaEm!.Value, ultima.FinalizadaEm!.Value, TimeSpan.FromMicroseconds(1));
    }

    [Fact]
    public async Task FalhaNoAsanaRegistraErroEMantemEmpresasJaProcessadasAsync()
    {
        Abertas(Workspaces[0].Gid, TarefaAsana("gravada"));
        Abertas(Workspaces[1].Gid, TarefaAsana("perdida"));
        Asana.Falha = (Workspaces[1].Gid, new AsanaIndisponivelException());

        var erro = await Assert.ThrowsAsync<AsanaIndisponivelException>(SincronizarAsync);

        Assert.NotNull(await LerTarefaAsync("gravada"));
        Assert.NotNull((await LerEmpresaAsync(Workspaces[0].Gid)).CargaInicialEm);
        Assert.Null(await LerTarefaAsync("perdida"));
        Assert.Null((await LerEmpresaAsync(Workspaces[1].Gid)).CargaInicialEm);

        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var ultima = await CriarServico(contexto).ObterUltimaAsync(TestContext.Current.CancellationToken);
        Assert.False(ultima!.Sucesso);
        Assert.Equal(erro.Message, ultima.Erro);
        Assert.Equal(1, ultima.TarefasNovas);
        Assert.NotNull(ultima.FinalizadaEm);

        // Idempotente: resolvida a falha, basta sincronizar de novo.
        Asana.Falha = null;
        var nova = await SincronizarAsync();
        Assert.True(nova.Sincronizacao!.Sucesso);
        Assert.Equal(1, nova.Sincronizacao.TarefasNovas);
        Assert.NotNull(await LerTarefaAsync("perdida"));
    }

    [Fact]
    public async Task SincronizacaoSimultaneaRespondeEmAndamentoAsync()
    {
        var liberar = new TaskCompletionSource();
        Asana.BloquearListagem = liberar.Task;

        var primeira = SincronizarAsync();
        await Asana.ListagemIniciada.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var segunda = await SincronizarAsync();
        Assert.Equal(SincronizacaoResultadoStatus.EmAndamento, segunda.Status);
        Assert.Null(segunda.Sincronizacao);

        liberar.SetResult();
        Assert.Equal(SincronizacaoResultadoStatus.Concluida, (await primeira).Status);

        Asana.BloquearListagem = null;
        Assert.Equal(SincronizacaoResultadoStatus.Concluida, (await SincronizarAsync()).Status);
    }
}
