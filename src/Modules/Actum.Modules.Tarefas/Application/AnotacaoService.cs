using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// <see cref="IAnotacaoService"/> over <see cref="TarefasDbContext"/>; nothing is sent to the Asana
/// </summary>
internal sealed class AnotacaoService(TarefasDbContext context, TimeProvider timeProvider) : IAnotacaoService
{
    public async Task<IReadOnlyList<AnotacaoResponse>?> ListarAsync(Guid tarefaIdentifier, CancellationToken cancellationToken)
    {
        var tarefaId = await TarefaIdAsync(tarefaIdentifier, cancellationToken);
        if (tarefaId is null)
        {
            return null;
        }

        var anotacoes = await context.Set<Anotacao>()
            .AsNoTracking()
            .Where(anotacao => anotacao.TarefaId == tarefaId)
            .OrderBy(anotacao => anotacao.CreateIn)
            .ThenBy(anotacao => anotacao.Id)
            .ToListAsync(cancellationToken);

        return anotacoes.Select(ParaResposta).ToList();
    }

    public async Task<AnotacaoResponse?> CriarAsync(Guid tarefaIdentifier, string texto, CancellationToken cancellationToken)
    {
        var tarefaId = await TarefaIdAsync(tarefaIdentifier, cancellationToken);
        if (tarefaId is null)
        {
            return null;
        }

        var anotacao = Anotacao.Criar(tarefaId.Value, Limpar(texto));
        context.Add(anotacao);
        await context.SaveChangesAsync(cancellationToken);

        return ParaResposta(anotacao);
    }

    public async Task<AnotacaoResponse?> AlterarAsync(Guid tarefaIdentifier, Guid anotacaoIdentifier, string texto, CancellationToken cancellationToken)
    {
        var anotacao = await AnotacaoDaTarefaAsync(tarefaIdentifier, anotacaoIdentifier, cancellationToken);
        if (anotacao is null)
        {
            return null;
        }

        anotacao.Alterar(Limpar(texto), timeProvider.GetLocalNow().DateTime);
        await context.SaveChangesAsync(cancellationToken);

        return ParaResposta(anotacao);
    }

    public async Task<bool> ExcluirAsync(Guid tarefaIdentifier, Guid anotacaoIdentifier, CancellationToken cancellationToken)
    {
        var anotacao = await AnotacaoDaTarefaAsync(tarefaIdentifier, anotacaoIdentifier, cancellationToken);
        if (anotacao is null)
        {
            return false;
        }

        context.Remove(anotacao);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Limpar(string texto)
    {
        var limpo = texto.Trim();
        if (limpo.Length == 0)
        {
            throw new DomainException(AnotacaoRequest.TextoObrigatorio);
        }

        return limpo;
    }

    private Task<Anotacao?> AnotacaoDaTarefaAsync(Guid tarefaIdentifier, Guid anotacaoIdentifier, CancellationToken cancellationToken)
    {
        return context.Set<Anotacao>()
            .Join(context.Set<Tarefa>(), anotacao => anotacao.TarefaId, tarefa => tarefa.Id, (anotacao, tarefa) => new { anotacao, tarefa })
            .Where(item => item.tarefa.Identifier == tarefaIdentifier && item.anotacao.Identifier == anotacaoIdentifier)
            .Select(item => item.anotacao)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<long?> TarefaIdAsync(Guid tarefaIdentifier, CancellationToken cancellationToken)
    {
        return await context.Set<Tarefa>()
            .Where(tarefa => tarefa.Identifier == tarefaIdentifier)
            .Select(tarefa => (long?)tarefa.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static AnotacaoResponse ParaResposta(Anotacao anotacao)
    {
        return new AnotacaoResponse(anotacao.Identifier, anotacao.Texto, anotacao.CreateIn, anotacao.AlteradaEm);
    }
}
