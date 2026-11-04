using Actum.Modules.Asana.Application;
using Actum.Modules.Asana.Errors;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// <see cref="IRetrabalhoService"/> over <see cref="TarefasDbContext"/> and the public <see cref="IAsanaClient"/>
/// contract of the Asana module
/// </summary>
internal sealed class RetrabalhoService(
    TarefasDbContext context,
    IAsanaClient asana,
    SincronizacaoEmAndamento emAndamento,
    ILogger<RetrabalhoService> logger) : IRetrabalhoService
{
    public const int TamanhoDoRanking = 10;

    public async Task<ReprocessamentoResultado> ReprocessarAsync(CancellationToken cancellationToken)
    {
        if (!emAndamento.TentarIniciar())
        {
            return new ReprocessamentoResultado(true);
        }

        var registro = new Sincronizacao(DateTime.Now, TipoSincronizacao.Reprocessamento);
        try
        {
            context.Add(registro);
            await context.SaveChangesAsync(cancellationToken);

            var antes = await context.Set<Retrabalho>().CountAsync(cancellationToken);
            var identificadores = await context.Set<Tarefa>()
                .OrderBy(tarefa => tarefa.Id)
                .Select(tarefa => tarefa.Id)
                .ToListAsync(cancellationToken);

            var falhas = new List<FalhaDoReprocessamentoResponse>();
            var lidas = 0;
            foreach (var id in identificadores)
            {
                var tarefa = await context.Set<Tarefa>()
                    .Include(tarefa => tarefa.Retrabalhos)
                    .Include(tarefa => tarefa.Conclusoes)
                    .Include(tarefa => tarefa.Branches)
                    .AsSplitQuery()
                    .SingleAsync(tarefa => tarefa.Id == id, cancellationToken);

                try
                {
                    var stories = await asana.ListarStoriesAsync(tarefa.AsanaGid, cancellationToken);
                    registro.Reaberturas += tarefa.RecalcularRetrabalhos(stories, voltouParaALista: false);
                    await context.SaveChangesAsync(cancellationToken);
                    lidas++;
                }
                catch (AsanaIndisponivelException erro)
                {
                    // Uma tarefa que o Asana não devolve (excluída ou indisponível) mantém os retrabalhos que tinha.
                    logger.LogWarning(erro, "Histórico da tarefa {Tarefa} não lido no reprocessamento", tarefa.Identifier);
                    falhas.Add(new FalhaDoReprocessamentoResponse(tarefa.Identifier, tarefa.Titulo, erro.Message));
                }
                finally
                {
                    context.ChangeTracker.Clear();
                }
            }

            var depois = await context.Set<Retrabalho>().CountAsync(cancellationToken);

            var concluido = await context.Set<Sincronizacao>().SingleAsync(s => s.Id == registro.Id, cancellationToken);
            concluido.Reaberturas = registro.Reaberturas;
            concluido.TarefasAtualizadas = lidas;
            concluido.Concluir(DateTime.Now);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Histórico reprocessado: {Lidas} tarefas lidas, retrabalhos {Antes} → {Depois}, {Falhas} falhas",
                lidas, antes, depois, falhas.Count);

            return new ReprocessamentoResultado(false, new ReprocessamentoResponse(lidas, antes, depois, falhas));
        }
        catch (Exception erro) when (registro.Id != 0)
        {
            await RegistrarFalhaAsync(registro.Id, erro);
            throw;
        }
        finally
        {
            emAndamento.Finalizar();
        }
    }

    public async Task<IndicadoresResponse?> ObterIndicadoresAsync(DateOnly de, DateOnly ate, Guid? empresa, CancellationToken cancellationToken)
    {
        var empresas = await context.Set<Empresa>()
            .AsNoTracking()
            .Where(e => empresa == null || e.Identifier == empresa)
            .OrderBy(e => e.Nome)
            .ToListAsync(cancellationToken);
        if (empresa is not null && empresas.Count == 0)
        {
            return null;
        }

        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var idsDasEmpresas = empresas.Select(e => e.Id).ToList();

        var retrabalhos = await context.Set<Retrabalho>()
            .AsNoTracking()
            .Join(context.Set<Tarefa>(), retrabalho => retrabalho.TarefaId, tarefa => tarefa.Id, (retrabalho, tarefa) => new { retrabalho, tarefa })
            .Where(item => idsDasEmpresas.Contains(item.tarefa.EmpresaId))
            .Where(item => item.retrabalho.ReabertaEm >= inicio && item.retrabalho.ReabertaEm < fim)
            .Select(item => new RetrabalhoDoPeriodo(
                item.tarefa.Id,
                item.tarefa.Identifier,
                item.tarefa.Titulo,
                item.tarefa.Status,
                item.tarefa.EmpresaId,
                item.retrabalho.ReabertaEm,
                item.retrabalho.ConcluidaEm))
            .ToListAsync(cancellationToken);

        var concluidas = await context.Set<Conclusao>()
            .AsNoTracking()
            .Join(context.Set<Tarefa>(), conclusao => conclusao.TarefaId, tarefa => tarefa.Id, (conclusao, tarefa) => new { conclusao, tarefa })
            .Where(item => idsDasEmpresas.Contains(item.tarefa.EmpresaId))
            .Where(item => item.conclusao.ConcluidaEm >= inicio && item.conclusao.ConcluidaEm < fim)
            .Select(item => new { item.tarefa.Id, item.tarefa.EmpresaId })
            .Distinct()
            .ToListAsync(cancellationToken);

        // Taxa: tarefas concluídas no período que voltaram depois de uma conclusão do período (o par começa nela).
        var reabertasAposConclusaoDoPeriodo = await context.Set<Retrabalho>()
            .AsNoTracking()
            .Where(retrabalho => retrabalho.ConcluidaEm >= inicio && retrabalho.ConcluidaEm < fim)
            .Select(retrabalho => retrabalho.TarefaId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var reabertas = reabertasAposConclusaoDoPeriodo.ToHashSet();

        IndicadorDaEmpresaResponse Indicador(Guid? identifier, string nome, string? cor, Func<long, bool> daEmpresa)
        {
            var doRetrabalho = retrabalhos.Where(item => daEmpresa(item.EmpresaId)).ToList();
            var concluidasDaEmpresa = concluidas.Where(item => daEmpresa(item.EmpresaId)).Select(item => item.Id).ToList();
            var voltaram = concluidasDaEmpresa.Count(reabertas.Contains);
            decimal? taxa = concluidasDaEmpresa.Count == 0
                ? null
                : Math.Round(voltaram * 100m / concluidasDaEmpresa.Count, 1, MidpointRounding.AwayFromZero);

            return new IndicadorDaEmpresaResponse(
                identifier,
                nome,
                cor,
                doRetrabalho.Count,
                doRetrabalho.Select(item => item.TarefaId).Distinct().Count(),
                concluidasDaEmpresa.Count,
                taxa);
        }

        var porEmpresa = empresas
            .Select(e => Indicador(e.Identifier, e.Nome, e.Cor, id => id == e.Id))
            .ToList();
        var total = Indicador(null, "Total", null, _ => true);

        var evolucao = new List<MesDeRetrabalhoResponse>();
        for (var mes = new DateOnly(de.Year, de.Month, 1); mes <= ate; mes = mes.AddMonths(1))
        {
            var doMes = retrabalhos.Where(item => item.ReabertaEm.Year == mes.Year && item.ReabertaEm.Month == mes.Month).ToList();
            evolucao.Add(new MesDeRetrabalhoResponse(
                $"{mes:yyyy-MM}",
                empresas.Select(e => new RetrabalhosDaEmpresaNoMesResponse(e.Identifier, doMes.Count(item => item.EmpresaId == e.Id))).ToList()));
        }

        var empresasPorId = empresas.ToDictionary(e => e.Id);
        var ranking = retrabalhos
            .GroupBy(item => item.TarefaId)
            .Select(grupo => new
            {
                Primeiro = grupo.First(),
                Quantidade = grupo.Count(),
                Ultima = grupo.Max(item => item.ReabertaEm),
            })
            .OrderByDescending(item => item.Quantidade)
            .ThenByDescending(item => item.Ultima)
            .Take(TamanhoDoRanking)
            .Select(item =>
            {
                var empresaDaTarefa = empresasPorId[item.Primeiro.EmpresaId];
                return new TarefaDoRankingResponse(
                    item.Primeiro.TarefaIdentifier,
                    item.Primeiro.Titulo,
                    new EmpresaResumoResponse(empresaDaTarefa.Identifier, empresaDaTarefa.Nome, empresaDaTarefa.Cor),
                    item.Primeiro.Status,
                    item.Quantidade,
                    item.Ultima);
            })
            .ToList();

        var ultimoReprocessamento = await context.Set<Sincronizacao>()
            .AsNoTracking()
            .Where(s => s.Tipo == TipoSincronizacao.Reprocessamento && s.Sucesso)
            .MaxAsync(s => s.FinalizadaEm, cancellationToken);

        return new IndicadoresResponse(de, ate, porEmpresa, total, evolucao, ranking, ultimoReprocessamento);
    }

    /// <summary>
    /// Record the reprocessing as failed (token missing or rejected, cancellation); the tasks already saved stay
    /// </summary>
    private async Task RegistrarFalhaAsync(long registroId, Exception erro)
    {
        try
        {
            context.ChangeTracker.Clear();
            var registro = await context.Set<Sincronizacao>().SingleAsync(s => s.Id == registroId, CancellationToken.None);
            registro.Falhar(DateTime.Now, erro is AsanaException asanaException ? asanaException.Message : "O reprocessamento foi interrompido.");
            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception falhaAoRegistrar)
        {
            logger.LogError(falhaAoRegistrar, "Não foi possível registrar a falha do reprocessamento {Id}", registroId);
        }
    }

    private sealed record RetrabalhoDoPeriodo(
        long TarefaId,
        Guid TarefaIdentifier,
        string Titulo,
        StatusTarefa Status,
        long EmpresaId,
        DateTime ReabertaEm,
        DateTime? ConcluidaEm);
}
