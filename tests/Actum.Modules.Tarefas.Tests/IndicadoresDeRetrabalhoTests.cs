using Actum.Modules.Asana.Contracts;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Persistence;
using Actum.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da capability <c>rework-dashboard</c>. O período (2030) não tem dados de outros cenários do banco
/// compartilhado.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class IndicadoresDeRetrabalhoTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP", "aremas.com.br")
{
    private static readonly DateOnly De = new(2030, 1, 1);

    private static readonly DateOnly Ate = new(2030, 3, 31);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TotaisTaxaEvolucaoERankingPorEmpresaAsync()
    {
        await MontarCenarioAsync();

        var indicadores = await IndicadoresAsync(null);

        var idAureum = (await LerEmpresaAsync(Workspaces[0].Gid)).Identifier;
        var idAremas = (await LerEmpresaAsync(Workspaces[1].Gid)).Identifier;
        var aureum = indicadores.Empresas.Single(e => e.Identifier == idAureum);
        var aremas = indicadores.Empresas.Single(e => e.Identifier == idAremas);
        Assert.Equal((3, 2, 4, (decimal?)50.0m), (aureum.Retrabalhos, aureum.TarefasComRetrabalho, aureum.TarefasConcluidas, aureum.Taxa));
        Assert.Equal((0, 0, 2, (decimal?)0.0m), (aremas.Retrabalhos, aremas.TarefasComRetrabalho, aremas.TarefasConcluidas, aremas.Taxa));
        // O período é compartilhado com os outros cenários desta classe: o total é conferido como soma das empresas.
        Assert.Equal(indicadores.Empresas.Sum(e => e.Retrabalhos), indicadores.Total.Retrabalhos);
        Assert.Equal(indicadores.Empresas.Sum(e => e.TarefasConcluidas), indicadores.Total.TarefasConcluidas);
        Assert.Equal(indicadores.Empresas.Sum(e => e.TarefasComRetrabalho), indicadores.Total.TarefasComRetrabalho);

        Assert.Equal(["2030-01", "2030-02", "2030-03"], indicadores.Evolucao.Select(mes => mes.Mes));
        Assert.Equal(
            [1, 0, 2],
            indicadores.Evolucao.Select(mes => mes.PorEmpresa.Single(e => e.Identifier == aureum.Identifier).Quantidade));
        Assert.All(indicadores.Evolucao, mes => Assert.Equal(0, mes.PorEmpresa.Single(e => e.Identifier == aremas.Identifier).Quantidade));

        Assert.Equal([("Tarefa a", 2), ("Tarefa b", 1)], indicadores.Ranking.Select(item => (item.Titulo, item.Retrabalhos)));
        Assert.Equal(new DateTime(2030, 3, 20, 10, 0, 0), indicadores.Ranking[0].UltimaReaberturaEm);
        Assert.NotNull(indicadores.UltimoReprocessamentoEm);
    }

    [Fact]
    public async Task FiltroPorEmpresaEEmpresaInexistenteAsync()
    {
        await MontarCenarioAsync();
        var aremas = await LerEmpresaAsync(Workspaces[1].Gid);

        var indicadores = await IndicadoresAsync(aremas.Identifier);

        var unica = Assert.Single(indicadores.Empresas);
        Assert.Equal(aremas.Identifier, unica.Identifier);
        Assert.Equal(0, indicadores.Total.Retrabalhos);
        Assert.Equal((2, (decimal?)0.0m), (indicadores.Total.TarefasConcluidas, indicadores.Total.Taxa));
        Assert.Empty(indicadores.Ranking);

        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        Assert.Null(await Servico(contexto).ObterIndicadoresAsync(De, Ate, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task EmpresaSemConclusoesTemTaxaNulaAsync()
    {
        await ConcluirCargaInicialAsync();

        var indicadores = await IndicadoresAsync((await LerEmpresaAsync(Workspaces[1].Gid)).Identifier);

        Assert.Null(indicadores.Total.Taxa);
        Assert.Equal(0, indicadores.Total.TarefasConcluidas);
    }

    [Fact]
    public async Task RankingDesempataPelaReaberturaMaisRecenteAsync()
    {
        await ConcluirCargaInicialAsync();
        Abertas(Workspaces[0].Gid, TarefaAsana("x"), TarefaAsana("y"));
        await SincronizarAsync();
        Historico("x", Conclusao("cx", "R", Data(1, 5)), Reabertura("rx", "M", Data(1, 6)));
        Historico("y", Conclusao("cy", "R", Data(2, 5)), Reabertura("ry", "M", Data(2, 6)));
        await ReprocessarAsync();

        var indicadores = await IndicadoresAsync((await LerEmpresaAsync(Workspaces[0].Gid)).Identifier);

        Assert.Equal(["Tarefa y", "Tarefa x"], indicadores.Ranking.Select(item => item.Titulo));
    }

    /// <summary>
    /// Aureum-ERP: "a" concluída e reaberta duas vezes (jan e mar), "b" uma vez (mar), "c" e "d" concluídas sem
    /// reabertura. aremas.com.br: "e" e "f" concluídas sem reabertura.
    /// </summary>
    private async Task MontarCenarioAsync()
    {
        await ConcluirCargaInicialAsync();
        Abertas(Workspaces[0].Gid, TarefaAsana("a"), TarefaAsana("b"), TarefaAsana("c"), TarefaAsana("d"));
        Abertas(Workspaces[1].Gid, TarefaAsana("e"), TarefaAsana("f"));
        await SincronizarAsync();

        Historico("a", Conclusao("ca1", "R", Data(1, 10)), Reabertura("ra1", "Maria", Data(1, 12)), Conclusao("ca2", "R", Data(3, 18)), Reabertura("ra2", "Maria", Data(3, 20)));
        Historico("b", Conclusao("cb1", "R", Data(3, 1)), Reabertura("rb1", "João", Data(3, 2)));
        Historico("c", Conclusao("cc1", "R", Data(2, 1)));
        Historico("d", Conclusao("cd1", "R", Data(2, 3)));
        Historico("e", Conclusao("ce1", "R", Data(1, 15)));
        Historico("f", Conclusao("cf1", "R", Data(3, 30)));

        await ReprocessarAsync();
    }

    private static DateTime Data(int mes, int dia) => new(2030, mes, dia, 10, 0, 0);

    private async Task ReprocessarAsync()
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        await Servico(contexto).ReprocessarAsync(Ct);
    }

    private async Task<IndicadoresResponse> IndicadoresAsync(Guid? empresa)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return (await Servico(contexto).ObterIndicadoresAsync(De, Ate, empresa, Ct))!;
    }

    private RetrabalhoService Servico(TarefasDbContext contexto)
    {
        return new RetrabalhoService(contexto, Asana, new SincronizacaoEmAndamento(), NullLogger<RetrabalhoService>.Instance);
    }
}
