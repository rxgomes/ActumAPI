using Actum.Modules.Asana.Errors;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da regra de retrabalho por pares (rework-tracking) e do reprocessamento (rework-dashboard).
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class RetrabalhoPorParesTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP")
{
    private const string Nome = "pares";

    private static readonly DateTime Antes = new(2026, 8, 1, 9, 0, 0);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CicloAntigoEmAbertoPoeATarefaEmRetrabalhoSemMudarOPrazoAsync()
    {
        Abertas(TarefaAsana(Nome, prazo: new DateOnly(2026, 11, 10)));
        await SincronizarAsync();

        // Ciclos anteriores ao Actum só aparecem no reprocessamento.
        Historico(Nome, Conclusao("c1", "Ricardo", Antes), Reabertura("r1", "Maria", Antes.AddHours(2)), Conclusao("c2", "Ricardo", Antes.AddDays(1)), Reabertura("r2", "João", Antes.AddDays(2)));
        var resumo = await ReprocessarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.Retrabalho, tarefa!.Status);
        Assert.False(tarefa.PrazoInternoPendente);
        Assert.Equal(new DateOnly(2026, 11, 10), tarefa.PrazoInterno);
        var retrabalhos = tarefa.Retrabalhos.OrderBy(r => r.Numero).ToList();
        Assert.Equal(
            [((short)1, "Maria", (DateTime?)Antes, (DateTime?)Antes.AddDays(1)), ((short)2, "João", (DateTime?)Antes.AddDays(1), (DateTime?)null)],
            retrabalhos.Select(r => (r.Numero, r.ReabertaPor, r.ConcluidaEm, r.ResolvidoEm)));
        Assert.All(retrabalhos, r => Assert.Null(r.PrazoInternoAnterior));
        Assert.Equal(2, (await ConclusoesAsync()).Count);
        Assert.Equal((1, 0, 2), (resumo.TarefasLidas, resumo.RetrabalhosAntes, resumo.RetrabalhosDepois));
    }

    [Fact]
    public async Task CiclosAntigosResolvidosMantemATarefaEmAndamentoAsync()
    {
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();

        Historico(Nome, Conclusao("c1", "Ricardo", Antes), Reabertura("r1", "Maria", Antes.AddHours(2)), Conclusao("c2", "Ricardo", Antes.AddDays(1)));
        await ReprocessarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.EmAndamento, tarefa!.Status);
        Assert.NotNull(Assert.Single(tarefa.Retrabalhos).ResolvidoEm);
    }

    [Fact]
    public async Task TarefaBloqueadaComRetrabalhoEmAbertoVoltaParaRetrabalhoAoDesbloquearAsync()
    {
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();
        await AlterarTarefaAsync(Nome, status: StatusTarefa.Bloqueada);
        await using (var contexto = TarefasDbContext.Criar(Postgres.DataSource))
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync($"update tarefas.tarefa set status_anterior = 'em_andamento' where asana_gid = {Gid(Nome)}", Ct);
        }

        Historico(Nome, Conclusao("c1", "Ricardo", Antes), Reabertura("r1", "Maria", Antes.AddHours(2)));
        await ReprocessarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        Assert.Equal((StatusTarefa.Bloqueada, (StatusTarefa?)StatusTarefa.Retrabalho), (tarefa!.Status, tarefa.StatusAnterior));
    }

    [Fact]
    public async Task TarefaNovaComHistoricoFicaNaEntradaComRetrabalhoAsync()
    {
        await ConcluirCargaInicialAsync();
        Abertas(TarefaAsana(Nome));
        Historico(Nome, Conclusao("c1", "Ana", Antes), Reabertura("r1", "Maria", Antes.AddHours(1)));

        await SincronizarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.Entrada, tarefa!.Status);
        Assert.False(tarefa.PrazoInternoPendente);
        Assert.Single(tarefa.Retrabalhos);
    }

    [Fact]
    public async Task ReaberturaNovaDeTarefaConcluidaReabreComEfeitosAsync()
    {
        Abertas(TarefaAsana(Nome, prazo: new DateOnly(2026, 10, 20)));
        await SincronizarAsync();
        var concluidaEm = Base.AddDays(1);
        await AlterarTarefaAsync(Nome, status: StatusTarefa.Concluida, concluidaEm: concluidaEm);
        ConcluiuNoAsana(Nome, concluidaEm);
        Abertas();
        Asana.Tarefas[Gid(Nome)] = TarefaAsana(Nome, concluida: true, modificadaEm: concluidaEm);
        await SincronizarAsync();

        var reabertaEm = concluidaEm.AddHours(3);
        Abertas(TarefaAsana(Nome, modificadaEm: reabertaEm));
        Historico(Nome, Reabertura("r1", "Maria", reabertaEm));
        var resultado = await SincronizarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        Assert.Equal((StatusTarefa.Retrabalho, true), (tarefa!.Status, tarefa.PrazoInternoPendente));
        var retrabalho = Assert.Single(tarefa.Retrabalhos);
        Assert.Equal((new DateOnly(2026, 10, 20), (DateTime?)concluidaEm), (retrabalho.PrazoInternoAnterior!.Value, retrabalho.ConcluidaEm));
        Assert.Equal(1, resultado.Sincronizacao!.Reaberturas);
    }

    [Fact]
    public async Task RetrabalhoSemParERemovidoEABranchContinuaSemVinculoAsync()
    {
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();
        var tarefaId = (await LerTarefaAsync(Nome))!.Id;

        // Dado da regra antiga: retrabalho "sem histórico" com uma branch ligada a ele.
        await using (var contexto = TarefasDbContext.Criar(Postgres.DataSource))
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"insert into tarefas.retrabalho (tarefa_id, numero, reaberta_em) values ({tarefaId}, 1, {Antes.ToString("yyyy-MM-dd HH:mm:ss")}::timestamp)", Ct);
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"insert into tarefas.tarefa_branch (tarefa_id, tipo, nome, retrabalho_id) select {tarefaId}, 'implementacao', 'fix/antigo', id from tarefas.retrabalho where tarefa_id = {tarefaId}", Ct);
        }

        Historico(Nome);
        var resumo = await ReprocessarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        Assert.Empty(tarefa!.Retrabalhos);
        var branch = Assert.Single(tarefa.Branches);
        Assert.Equal(("fix/antigo", (long?)null), (branch.Nome, branch.RetrabalhoId));
        Assert.Equal((1, 0), (resumo.RetrabalhosAntes, resumo.RetrabalhosDepois));
    }

    [Fact]
    public async Task RetrabalhoExistentePreservaDadosEERenumeradoAsync()
    {
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();
        var recente = Antes.AddDays(10);
        Historico(Nome, Conclusao("c2", "Ricardo", recente.AddHours(-1)), Reabertura("r2", "Maria", recente));
        await ReprocessarAsync();
        var original = Assert.Single((await LerTarefaAsync(Nome))!.Retrabalhos);
        await using (var contexto = TarefasDbContext.Criar(Postgres.DataSource))
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync(
                $"update tarefas.retrabalho set prazo_interno_novo = {new DateOnly(2026, 11, 7)}, resolvido_em = {recente.AddDays(1).ToString("yyyy-MM-dd HH:mm:ss")}::timestamp where id = {original.Id}", Ct);
        }

        // Um par mais antigo aparece no histórico: o existente vira o número 2, com os mesmos dados do Actum.
        Historico(Nome, Conclusao("c1", "Ricardo", Antes), Reabertura("r1", "João", Antes.AddHours(1)), Conclusao("c2", "Ricardo", recente.AddHours(-1)), Reabertura("r2", "Maria", recente));
        await ReprocessarAsync();
        await ReprocessarAsync();

        var retrabalhos = (await LerTarefaAsync(Nome))!.Retrabalhos.OrderBy(r => r.Numero).ToList();
        Assert.Equal([(short)1, (short)2], retrabalhos.Select(r => r.Numero));
        var preservado = retrabalhos[1];
        Assert.Equal(
            (original.Identifier, (DateOnly?)new DateOnly(2026, 11, 7), (DateTime?)recente.AddDays(1)),
            (preservado.Identifier, preservado.PrazoInternoNovo, preservado.ResolvidoEm));
        Assert.Equal(2, (await ConclusoesAsync()).Count);
    }

    [Fact]
    public async Task ReprocessamentoConcorrenteRecebeEmAndamentoAsync()
    {
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();
        Asana.StoriesConsultadas.Clear();
        var trava = new SincronizacaoEmAndamento();
        Assert.True(trava.TentarIniciar());

        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var resultado = await new RetrabalhoService(contexto, Asana, trava, NullLogger<RetrabalhoService>.Instance).ReprocessarAsync(Ct);

        Assert.True(resultado.EmAndamento);
        Assert.Empty(Asana.StoriesConsultadas);
    }

    [Fact]
    public async Task FalhaDeUmaTarefaNaoInterrompeOReprocessamentoAsync()
    {
        Abertas(TarefaAsana(Nome), TarefaAsana("outra"));
        await SincronizarAsync();
        Historico("outra", Conclusao("c1", "Ricardo", Antes), Reabertura("r1", "Maria", Antes.AddHours(1)));
        Asana.FalharStories = Gid(Nome);

        var resumo = await ReprocessarAsync();

        var falha = Assert.Single(resumo.Falhas);
        Assert.Equal($"Tarefa {Nome}", falha.Titulo);
        Assert.Single((await LerTarefaAsync("outra"))!.Retrabalhos);
    }

    /// <summary>
    /// Reprocessa e confere o resumo com o banco. O banco de teste é compartilhado, então os totais globais são
    /// conferidos pela contagem real; o resumo devolvido traz só a variação deste cenário em "Antes" e "Depois".
    /// </summary>
    private async Task<Actum.Modules.Tarefas.Contracts.ReprocessamentoResponse> ReprocessarAsync()
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        var antes = await contexto.Set<Retrabalho>().CountAsync(Ct);
        var tarefas = await contexto.Set<Tarefa>().CountAsync(Ct);
        var meus = await RetrabalhosDoCenarioAsync(contexto);

        var resultado = await new RetrabalhoService(contexto, Asana, new SincronizacaoEmAndamento(), NullLogger<RetrabalhoService>.Instance).ReprocessarAsync(Ct);

        var resumo = resultado.Resumo!;
        Assert.Equal(antes, resumo.RetrabalhosAntes);
        Assert.Equal(await contexto.Set<Retrabalho>().CountAsync(Ct), resumo.RetrabalhosDepois);
        Assert.Equal(tarefas, resumo.TarefasLidas + resumo.Falhas.Count);

        return resumo with { TarefasLidas = 1, RetrabalhosAntes = meus, RetrabalhosDepois = await RetrabalhosDoCenarioAsync(contexto) };
    }

    private async Task<int> RetrabalhosDoCenarioAsync(TarefasDbContext contexto)
    {
        var gids = new[] { Gid(Nome), Gid("outra") };
        return await contexto.Set<Retrabalho>()
            .Join(contexto.Set<Tarefa>(), retrabalho => retrabalho.TarefaId, tarefa => tarefa.Id, (retrabalho, tarefa) => tarefa.AsanaGid)
            .CountAsync(gid => gids.Contains(gid), Ct);
    }

    private async Task<List<Conclusao>> ConclusoesAsync()
    {
        var tarefa = await LerTarefaAsync(Nome);
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return await contexto.Set<Conclusao>().Where(conclusao => conclusao.TarefaId == tarefa!.Id).ToListAsync(Ct);
    }
}
