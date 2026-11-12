using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Worktree actions over <see cref="TarefasDbContext"/>; the git work is in <see cref="WorktreesLocais"/>
/// </summary>
internal sealed class WorktreeService(TarefasDbContext context, WorktreesLocais worktrees) : IWorktreeService
{
    public async Task<TarefaResultado> CriarAsync(Guid identifier, Guid branchIdentifier, CancellationToken cancellationToken)
    {
        var (tarefa, branch) = await CarregarAsync(identifier, branchIdentifier, cancellationToken);
        if (tarefa is null || branch is null)
        {
            return TarefaResultado.NaoEncontrada;
        }

        if (branch.Tipo != TipoBranch.Implementacao)
        {
            throw new DomainException($"Só branches de implementação têm worktree. A branch '{branch.Nome}' é base.");
        }

        var empresa = await EmpresaAsync(tarefa, cancellationToken);
        if (!empresa.TemConfiguracaoLocal(branch.Partes))
        {
            throw new DomainException(
                $"Configure a pasta dos worktrees e os clones locais de front e back da empresa {empresa.Nome} na página Empresas.");
        }

        await worktrees.CriarAsync(empresa, branch, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return TarefaResultado.Sucesso(TarefaCiclo.ParaResponse(tarefa), TarefaCiclo.ParaResponse(tarefa, branch));
    }

    public async Task<TarefaResultado> RemoverAsync(Guid identifier, Guid branchIdentifier, CancellationToken cancellationToken)
    {
        var (tarefa, branch) = await CarregarAsync(identifier, branchIdentifier, cancellationToken);
        if (tarefa is null || branch is not { TemWorktreeAtivo: true })
        {
            return TarefaResultado.NaoEncontrada;
        }

        var empresa = await EmpresaAsync(tarefa, cancellationToken);
        var pendencias = await worktrees.RemoverAsync(empresa, branch.Worktree!, token => context.SaveChangesAsync(token), cancellationToken);
        if (pendencias is not null)
        {
            return TarefaResultado.WorktreeComPendencias(pendencias);
        }

        return TarefaResultado.Sucesso(TarefaCiclo.ParaResponse(tarefa), TarefaCiclo.ParaResponse(tarefa, branch));
    }

    private async Task<(Tarefa? Tarefa, TarefaBranch? Branch)> CarregarAsync(Guid identifier, Guid branchIdentifier, CancellationToken cancellationToken)
    {
        var tarefa = await TarefaCiclo.CarregarAsync(context, identifier, cancellationToken);
        return (tarefa, tarefa?.Branches.FirstOrDefault(branch => branch.Identifier == branchIdentifier));
    }

    private Task<Empresa> EmpresaAsync(Tarefa tarefa, CancellationToken cancellationToken)
    {
        return context.Set<Empresa>()
            .AsNoTracking()
            .SingleAsync(empresa => empresa.Id == tarefa.EmpresaId, cancellationToken);
    }
}
