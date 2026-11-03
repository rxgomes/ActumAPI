using System.Collections.Concurrent;
using Actum.Modules.Claude.Application;
using Actum.Modules.Claude.Contracts;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Tasks with an analysis running (singleton). The Actum runs in a single process, so an in-memory set is enough.
/// </summary>
internal sealed class AnalisesEmAndamento
{
    private readonly ConcurrentDictionary<long, byte> _tarefas = new();

    /// <summary>
    /// Try to start the analysis of the task without waiting
    /// </summary>
    /// <returns><see langword="false"/> when an analysis of the task is already running</returns>
    public bool TentarIniciar(long tarefaId) => _tarefas.TryAdd(tarefaId, 0);

    /// <summary>
    /// Release the task taken by <see cref="TentarIniciar"/>
    /// </summary>
    public void Finalizar(long tarefaId) => _tarefas.TryRemove(tarefaId, out _);
}

/// <summary>
/// <see cref="IAnaliseService"/> over <see cref="TarefasDbContext"/> and the public <see cref="IAnalisadorDeDemanda"/>
/// contract of the Claude module
/// </summary>
internal sealed class AnaliseService(
    TarefasDbContext context,
    IAnalisadorDeDemanda analisador,
    AnalisesEmAndamento emAndamento,
    TimeProvider timeProvider) : IAnaliseService
{
    private const string TarefaArquivada = "Tarefas arquivadas não podem ser analisadas.";

    private const string ItemDeVersaoAnterior = "Só os itens da análise atual podem ser marcados.";

    private const string GrupoPadrao = "Geral";

    public async Task<AnaliseResultado> AnalisarAsync(Guid tarefaIdentifier, CancellationToken cancellationToken)
    {
        var tarefa = await context.Set<Tarefa>()
            .AsNoTracking()
            .Include(tarefa => tarefa.Comentarios)
            .Include(tarefa => tarefa.Retrabalhos)
            .AsSplitQuery()
            .SingleOrDefaultAsync(tarefa => tarefa.Identifier == tarefaIdentifier, cancellationToken);
        if (tarefa is null)
        {
            return AnaliseResultado.NaoEncontrada;
        }

        if (tarefa.Status == StatusTarefa.Arquivada)
        {
            throw new DomainException(TarefaArquivada);
        }

        if (!emAndamento.TentarIniciar(tarefa.Id))
        {
            return AnaliseResultado.EmAndamento;
        }

        try
        {
            var empresa = await context.Set<Empresa>()
                .Where(empresa => empresa.Id == tarefa.EmpresaId)
                .Select(empresa => empresa.Nome)
                .SingleAsync(cancellationToken);

            var demanda = new DemandaParaAnalise(
                empresa,
                tarefa.Titulo,
                tarefa.Descricao,
                tarefa.Projetos,
                tarefa.Comentarios
                    .OrderBy(comentario => comentario.CriadoEm)
                    .ThenBy(comentario => comentario.Id)
                    .Select(comentario => new ComentarioDaDemanda(comentario.Autor, comentario.CriadoEm, comentario.Texto))
                    .ToList(),
                tarefa.Retrabalhos
                    .OrderBy(retrabalho => retrabalho.Numero)
                    .Select(retrabalho => new RetrabalhoDaDemanda(retrabalho.Numero, retrabalho.ReabertaEm, retrabalho.ReabertaPor))
                    .ToList());

            // Fora de transação: a chamada à IA pode levar dezenas de segundos. Se falhar, nada é gravado.
            var estruturada = await analisador.AnalisarAsync(demanda, cancellationToken);

            var anterior = await AtualAsync(tarefa.Id, cancellationToken);
            var analise = Analise.Criar(
                tarefa.Id,
                anterior,
                estruturada.Objetivo,
                estruturada.Checklist.Select(item => ((string?)item.Grupo, item.Texto, item.Vago)).ToList(),
                estruturada.Testes,
                estruturada.Duvidas,
                estruturada.Modelo,
                estruturada.TokensEntrada,
                estruturada.TokensSaida,
                Agora());

            context.Add(analise);
            await context.SaveChangesAsync(cancellationToken);

            return new AnaliseResultado(AnaliseResultadoStatus.Sucesso, ParaResposta(analise, true));
        }
        finally
        {
            emAndamento.Finalizar(tarefa.Id);
        }
    }

    public async Task<IReadOnlyList<AnaliseResponse>?> ListarAsync(Guid tarefaIdentifier, CancellationToken cancellationToken)
    {
        var tarefaId = await TarefaIdAsync(tarefaIdentifier, cancellationToken);
        if (tarefaId is null)
        {
            return null;
        }

        var analises = await context.Set<Analise>()
            .AsNoTracking()
            .Include(analise => analise.Itens)
            .Where(analise => analise.TarefaId == tarefaId)
            .OrderByDescending(analise => analise.Versao)
            .ToListAsync(cancellationToken);

        return analises.Select((analise, indice) => ParaResposta(analise, indice == 0)).ToList();
    }

    public async Task<AnaliseResultado> MarcarItemAsync(Guid tarefaIdentifier, Guid itemIdentifier, bool concluido, CancellationToken cancellationToken)
    {
        var tarefaId = await TarefaIdAsync(tarefaIdentifier, cancellationToken);
        if (tarefaId is null)
        {
            return AnaliseResultado.NaoEncontrada;
        }

        var analise = await context.Set<Analise>()
            .Include(analise => analise.Itens)
            .Where(analise => analise.TarefaId == tarefaId && analise.Itens.Any(item => item.Identifier == itemIdentifier))
            .SingleOrDefaultAsync(cancellationToken);
        if (analise is null)
        {
            return AnaliseResultado.NaoEncontrada;
        }

        var ultimaVersao = await context.Set<Analise>()
            .Where(outra => outra.TarefaId == tarefaId)
            .MaxAsync(outra => outra.Versao, cancellationToken);
        if (analise.Versao != ultimaVersao)
        {
            throw new DomainException(ItemDeVersaoAnterior);
        }

        var item = analise.Itens.Single(item => item.Identifier == itemIdentifier);
        item.Marcar(concluido, Agora());
        await context.SaveChangesAsync(cancellationToken);

        return new AnaliseResultado(AnaliseResultadoStatus.Sucesso, Item: ParaResposta(item));
    }

    private Task<Analise?> AtualAsync(long tarefaId, CancellationToken cancellationToken)
    {
        return context.Set<Analise>()
            .AsNoTracking()
            .Include(analise => analise.Itens)
            .Where(analise => analise.TarefaId == tarefaId)
            .OrderByDescending(analise => analise.Versao)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<long?> TarefaIdAsync(Guid tarefaIdentifier, CancellationToken cancellationToken)
    {
        return await context.Set<Tarefa>()
            .Where(tarefa => tarefa.Identifier == tarefaIdentifier)
            .Select(tarefa => (long?)tarefa.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private DateTime Agora() => timeProvider.GetLocalNow().DateTime;

    private static AnaliseResponse ParaResposta(Analise analise, bool atual)
    {
        List<AnaliseItemResponse> Itens(TipoItemAnalise tipo) => analise.Itens
            .Where(item => item.Tipo == tipo)
            .OrderBy(item => item.Ordem)
            .Select(ParaResposta)
            .ToList();

        // GroupBy mantém a ordem da primeira ocorrência de cada grupo; análises antigas, sem grupo, ficam em "Geral".
        var checklist = Itens(TipoItemAnalise.Checklist)
            .GroupBy(item => item.Grupo ?? GrupoPadrao)
            .Select(grupo => new GrupoDoChecklistResponse(grupo.Key, grupo.ToList()))
            .ToList();

        return new AnaliseResponse(
            analise.Identifier,
            analise.Versao,
            atual,
            analise.Objetivo,
            checklist,
            Itens(TipoItemAnalise.Teste),
            Itens(TipoItemAnalise.Duvida),
            analise.Modelo,
            analise.TokensEntrada,
            analise.TokensSaida,
            analise.AnalisadaEm);
    }

    private static AnaliseItemResponse ParaResposta(AnaliseItem item)
    {
        return new AnaliseItemResponse(
            item.Identifier,
            item.Tipo,
            item.Ordem,
            item.Texto,
            item.Grupo,
            item.Vago,
            item.ConcluidoEm is not null,
            item.ConcluidoEm);
    }
}
