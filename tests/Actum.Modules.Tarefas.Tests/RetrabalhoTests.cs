using Actum.Modules.Tarefas.Domain;
using Actum.Tests.Shared;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários da capability <c>rework-tracking</c>. A conclusão pelo Actum (fora desta change) é simulada
/// alterando status e <c>concluida_em</c> direto no banco.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class RetrabalhoTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP")
{
    private const string Nome = "reaberta";

    private static readonly DateTime PrimeiraConclusao = Base.AddDays(1);

    [Fact]
    public async Task TarefaReabertaPeloTestadorGeraPrimeiroRetrabalhoAsync()
    {
        await PrepararTarefaConcluidaAsync(prazoInterno: new DateOnly(2026, 10, 20));
        var reabertaEm = PrimeiraConclusao.AddHours(5);

        Reabrir(modificadaEm: reabertaEm, Reabertura("r1", "Maria", reabertaEm));
        var resultado = await SincronizarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        var retrabalho = Assert.Single(tarefa!.Retrabalhos);
        Assert.Equal(1, retrabalho.Numero);
        Assert.Equal("Maria", retrabalho.ReabertaPor);
        Assert.Equal(reabertaEm, retrabalho.ReabertaEm);
        Assert.Equal(Gid("r1"), retrabalho.AsanaStoryGid);
        Assert.Null(retrabalho.PrazoInternoNovo);
        Assert.Null(retrabalho.ResolvidoEm);
        Assert.Equal(1, resultado.Sincronizacao!.Reaberturas);
    }

    [Fact]
    public async Task ReaberturaMudaStatusEMarcaPrazoPendenteGuardandoOPrazoAnteriorAsync()
    {
        await PrepararTarefaConcluidaAsync(prazoInterno: new DateOnly(2026, 10, 20));

        Reabrir(modificadaEm: PrimeiraConclusao.AddHours(5), Reabertura("r1", "Maria", PrimeiraConclusao.AddHours(5)));
        await SincronizarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.Retrabalho, tarefa!.Status);
        Assert.True(tarefa.PrazoInternoPendente);
        Assert.Equal(new DateOnly(2026, 10, 20), tarefa.PrazoInterno);
        Assert.Equal(new DateOnly(2026, 10, 20), Assert.Single(tarefa.Retrabalhos).PrazoInternoAnterior);
    }

    [Fact]
    public async Task SegundaReaberturaGeraNumero2EMantemAPrimeiraAsync()
    {
        await PrepararTarefaConcluidaAsync();
        var primeira = Reabertura("r1", "Maria", PrimeiraConclusao.AddHours(5));
        Reabrir(modificadaEm: PrimeiraConclusao.AddHours(5), primeira);
        await SincronizarAsync();

        var segundaConclusao = PrimeiraConclusao.AddDays(2);
        await ConcluirNoActumAsync(segundaConclusao);
        var segunda = Reabertura("r2", "João", segundaConclusao.AddHours(3));
        Reabrir(modificadaEm: segundaConclusao.AddHours(3), primeira, segunda);
        await SincronizarAsync();

        var retrabalhos = (await LerTarefaAsync(Nome))!.Retrabalhos.OrderBy(r => r.Numero).ToList();
        Assert.Equal([(short)1, (short)2], retrabalhos.Select(r => r.Numero));
        Assert.Equal(["Maria", "João"], retrabalhos.Select(r => r.ReabertaPor));
    }

    [Fact]
    public async Task ReaberturasSeguidasSemNovaConclusaoContamUmaVezAsync()
    {
        await PrepararTarefaConcluidaAsync();

        // Fora de ordem de propósito: o par é a primeira reabertura depois da conclusão.
        Reabrir(
            modificadaEm: PrimeiraConclusao.AddHours(9),
            Reabertura("r-tarde", "João", PrimeiraConclusao.AddHours(8)),
            Reabertura("r-cedo", "Maria", PrimeiraConclusao.AddHours(2)));
        await SincronizarAsync();

        var retrabalho = Assert.Single((await LerTarefaAsync(Nome))!.Retrabalhos);
        Assert.Equal(("Maria", Gid("r-cedo"), (DateTime?)PrimeiraConclusao), (retrabalho.ReabertaPor, retrabalho.AsanaStoryGid, retrabalho.ConcluidaEm));
    }

    [Fact]
    public async Task ReaberturaJaRegistradaNaoGeraNovoRetrabalhoAsync()
    {
        await PrepararTarefaConcluidaAsync();
        Reabrir(modificadaEm: PrimeiraConclusao.AddHours(5), Reabertura("r1", "Maria", PrimeiraConclusao.AddHours(5)));
        await SincronizarAsync();

        // Mesmo com nova modificação no Asana (ex.: um comentário), a reabertura já registrada não se repete.
        Reabrir(
            modificadaEm: PrimeiraConclusao.AddHours(6),
            Reabertura("r1", "Maria", PrimeiraConclusao.AddHours(5)),
            Comentario("c1", "Maria", "detalhe", PrimeiraConclusao.AddHours(6)));
        var resultado = await SincronizarAsync();
        await SincronizarAsync();

        Assert.Single((await LerTarefaAsync(Nome))!.Retrabalhos);
        Assert.Equal(0, resultado.Sincronizacao!.Reaberturas);
    }

    [Fact]
    public async Task ReaberturaSemRegistroNoHistoricoVoltaParaEmAndamentoSemRetrabalhoAsync()
    {
        await PrepararTarefaConcluidaAsync(prazoInterno: new DateOnly(2026, 10, 20));

        // Só uma reabertura anterior à conclusão, sem conclusão antes dela: não é par.
        Reabrir(modificadaEm: PrimeiraConclusao.AddHours(5), Reabertura("r-antiga", "Maria", PrimeiraConclusao.AddHours(-1)));
        var resultado = await SincronizarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        Assert.Empty(tarefa!.Retrabalhos);
        Assert.Equal(StatusTarefa.EmAndamento, tarefa.Status);
        Assert.True(tarefa.PrazoInternoPendente);
        Assert.Equal(0, resultado.Sincronizacao!.Reaberturas);
    }

    [Fact]
    public async Task ArquivadaPorConclusaoNoAsanaEReabertaViraRetrabalhoAsync()
    {
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();
        Abertas();
        Asana.Tarefas[Gid(Nome)] = TarefaAsana(Nome, concluida: true);
        ConcluiuNoAsana(Nome, Base.AddDays(1), "Maria");
        await SincronizarAsync();
        var arquivada = await LerTarefaAsync(Nome);
        Assert.Equal(MotivoArquivamento.ConcluidaNoAsana, arquivada!.MotivoArquivamento);

        var reabertaEm = arquivada.ArquivadaEm!.Value.AddMinutes(30);
        Reabrir(modificadaEm: reabertaEm, Reabertura("r1", "Maria", reabertaEm));
        var resultado = await SincronizarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.Retrabalho, tarefa!.Status);
        Assert.True(tarefa.PrazoInternoPendente);
        Assert.Null(tarefa.MotivoArquivamento);
        var retrabalho = Assert.Single(tarefa.Retrabalhos);
        Assert.Equal(("Maria", reabertaEm), (retrabalho.ReabertaPor, retrabalho.ReabertaEm));
        Assert.Equal(1, resultado.Sincronizacao!.Reaberturas);
    }

    [Fact]
    public async Task ArquivadaPorConclusaoNoAsanaQueVoltaSemReaberturaVaiParaEntradaAsync()
    {
        Abertas(TarefaAsana(Nome));
        await SincronizarAsync();
        Abertas();
        Asana.Tarefas[Gid(Nome)] = TarefaAsana(Nome, concluida: true);
        await SincronizarAsync();

        Reabrir(modificadaEm: Base.AddDays(5));
        await SincronizarAsync();

        var tarefa = await LerTarefaAsync(Nome);
        Assert.Equal(StatusTarefa.Entrada, tarefa!.Status);
        Assert.Empty(tarefa.Retrabalhos);
    }

    /// <summary>
    /// Cria a tarefa (carga inicial) e simula a conclusão pelo Actum em <see cref="PrimeiraConclusao"/>; ela sai da
    /// lista de abertas do Asana
    /// </summary>
    private async Task PrepararTarefaConcluidaAsync(DateOnly? prazoInterno = null)
    {
        Abertas(TarefaAsana(Nome, prazo: prazoInterno));
        await SincronizarAsync();
        await ConcluirNoActumAsync(PrimeiraConclusao);
    }

    private async Task ConcluirNoActumAsync(DateTime concluidaEm)
    {
        await AlterarTarefaAsync(Nome, status: StatusTarefa.Concluida, concluidaEm: concluidaEm);
        ConcluiuNoAsana(Nome, concluidaEm);
        Abertas();
        Asana.Tarefas[Gid(Nome)] = TarefaAsana(Nome, concluida: true, modificadaEm: concluidaEm);
        await SincronizarAsync();
    }

    /// <summary>
    /// A tarefa volta à lista de abertas do Asana com o histórico informado
    /// </summary>
    private void Reabrir(DateTime modificadaEm, params Actum.Modules.Asana.Contracts.AsanaStory[] historico)
    {
        Abertas(TarefaAsana(Nome, modificadaEm: modificadaEm));
        Historico(Nome, historico);
    }
}
