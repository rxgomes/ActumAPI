using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Task lifecycle actions over <see cref="TarefasDbContext"/>; the rules live in <see cref="Tarefa"/>
/// </summary>
internal sealed class TarefaService(TarefasDbContext context) : ITarefaService
{
    public Task<TarefaResultado> IniciarAsync(Guid identifier, IniciarTarefaRequest request, CancellationToken cancellationToken)
    {
        return AplicarAsync(
            identifier,
            tarefa => tarefa.Iniciar(request.BranchBase!, request.BranchImplementacao!, request.PrazoInterno!.Value),
            cancellationToken);
    }

    public Task<TarefaResultado> BloquearAsync(Guid identifier, CancellationToken cancellationToken)
    {
        return AplicarAsync(identifier, tarefa => tarefa.Bloquear(), cancellationToken);
    }

    public Task<TarefaResultado> DesbloquearAsync(Guid identifier, CancellationToken cancellationToken)
    {
        return AplicarAsync(identifier, tarefa => tarefa.Desbloquear(), cancellationToken);
    }

    public Task<TarefaResultado> AlterarAsync(Guid identifier, AlterarTarefaRequest request, CancellationToken cancellationToken)
    {
        return AplicarAsync(
            identifier,
            tarefa =>
            {
                if (request.PrazoInterno is { } prazo)
                {
                    tarefa.DefinirPrazoInterno(prazo);
                }

                if (request.Prioridade is { } prioridade)
                {
                    tarefa.DefinirPrioridade(prioridade);
                }
            },
            cancellationToken);
    }

    public async Task<TarefaResultado> AdicionarBranchAsync(Guid identifier, AdicionarBranchRequest request, CancellationToken cancellationToken)
    {
        var tarefa = await TarefaCiclo.CarregarAsync(context, identifier, cancellationToken);
        if (tarefa is null)
        {
            return TarefaResultado.NaoEncontrada;
        }

        var branch = tarefa.AdicionarBranch(request.Tipo!.Value, request.Nome!);
        if (branch is null)
        {
            return TarefaResultado.BranchRepetida;
        }

        await context.SaveChangesAsync(cancellationToken);

        return TarefaResultado.Sucesso(TarefaCiclo.ParaResponse(tarefa), TarefaCiclo.ParaResponse(tarefa, branch));
    }

    public async Task<TarefaResultado> RemoverBranchAsync(Guid identifier, Guid branchIdentifier, CancellationToken cancellationToken)
    {
        var tarefa = await TarefaCiclo.CarregarAsync(context, identifier, cancellationToken);
        if (tarefa is null || !tarefa.RemoverBranch(branchIdentifier))
        {
            return TarefaResultado.NaoEncontrada;
        }

        await context.SaveChangesAsync(cancellationToken);

        return TarefaResultado.Sucesso(TarefaCiclo.ParaResponse(tarefa));
    }

    private async Task<TarefaResultado> AplicarAsync(Guid identifier, Action<Tarefa> acao, CancellationToken cancellationToken)
    {
        var tarefa = await TarefaCiclo.CarregarAsync(context, identifier, cancellationToken);
        if (tarefa is null)
        {
            return TarefaResultado.NaoEncontrada;
        }

        acao(tarefa);
        await context.SaveChangesAsync(cancellationToken);

        return TarefaResultado.Sucesso(TarefaCiclo.ParaResponse(tarefa));
    }
}
