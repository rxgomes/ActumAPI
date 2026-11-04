using Actum.Modules.Asana.Contracts;
using Actum.Modules.Tarefas.Domain;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Pares concluiu → marcou como não concluída a partir do histórico, sem banco.
/// </summary>
public sealed class HistoricoDeConclusoesTests
{
    private static readonly DateTime Base = new(2026, 9, 1, 10, 0, 0);

    [Fact]
    public void UmCicloGeraUmRetrabalhoEmAberto()
    {
        var historico = HistoricoDeConclusoes.Calcular([Concluiu("c1", 0, "Ricardo"), Reabriu("r1", 2, "Maria")]);

        Assert.Equal([new ConclusaoDoHistorico("c1", Base, "Ricardo")], historico.Conclusoes);
        Assert.Equal([new ParDoHistorico(Base, "r1", Base.AddHours(2), "Maria", null)], historico.Pares);
    }

    [Fact]
    public void VariosCiclosResolvemPelaConclusaoSeguinte()
    {
        var historico = HistoricoDeConclusoes.Calcular(
            [Concluiu("c1", 0), Reabriu("r1", 1), Concluiu("c2", 5), Reabriu("r2", 6), Concluiu("c3", 9)]);

        Assert.Equal(3, historico.Conclusoes.Count);
        Assert.Equal(
            [("r1", (DateTime?)Base.AddHours(5), Base), ("r2", (DateTime?)Base.AddHours(9), Base.AddHours(5))],
            historico.Pares.Select(par => (par.ReaberturaGid, par.ResolvidoEm, par.ConcluidaEm)));
    }

    [Fact]
    public void ReaberturaSemConclusaoAntesNaoConta()
    {
        var historico = HistoricoDeConclusoes.Calcular([Reabriu("r0", 0), Concluiu("c1", 1), Reabriu("r1", 2), Reabriu("r2", 3)]);

        Assert.Equal(["r1"], historico.Pares.Select(par => par.ReaberturaGid));
    }

    [Fact]
    public void ConclusoesSeguidasUsamAMaisRecenteNoPar()
    {
        var historico = HistoricoDeConclusoes.Calcular([Concluiu("c1", 0), Concluiu("c2", 1), Reabriu("r1", 2)]);

        Assert.Equal(Base.AddHours(1), Assert.Single(historico.Pares).ConcluidaEm);
    }

    [Fact]
    public void OrdemDaApiNaoImportaEEmpateUsaOGid()
    {
        var historico = HistoricoDeConclusoes.Calcular([Reabriu("b-reabriu", 0), Concluiu("a-concluiu", 0), Comentou("x", 0)]);

        Assert.Equal(["b-reabriu"], historico.Pares.Select(par => par.ReaberturaGid));
    }

    private static AsanaStory Concluiu(string gid, int horas, string? autor = "Ricardo") =>
        new(gid, AsanaStoryTipo.MarcadaConcluida, Base.AddHours(horas), autor, "concluiu esta tarefa");

    private static AsanaStory Reabriu(string gid, int horas, string? autor = "Maria") =>
        new(gid, AsanaStoryTipo.MarcadaIncompleta, Base.AddHours(horas), autor, "marcou esta tarefa como não concluída");

    private static AsanaStory Comentou(string gid, int horas) =>
        new(gid, AsanaStoryTipo.Comentario, Base.AddHours(horas), "João", "comentário");
}
