using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Loading and mapping shared by the lifecycle services
/// </summary>
internal static class TarefaCiclo
{
    /// <summary>
    /// Load a task, tracked, with its branches and reworks
    /// </summary>
    public static Task<Tarefa?> CarregarAsync(TarefasDbContext context, Guid identifier, CancellationToken cancellationToken)
    {
        return context.Set<Tarefa>()
            .Include(tarefa => tarefa.Branches)
            .Include(tarefa => tarefa.Retrabalhos)
            .AsSplitQuery()
            .SingleOrDefaultAsync(tarefa => tarefa.Identifier == identifier, cancellationToken);
    }

    public static TarefaCicloResponse ParaResponse(Tarefa tarefa)
    {
        var branches = tarefa.Branches
            .OrderBy(branch => branch.Tipo)
            .ThenBy(branch => branch.Id)
            .Select(branch => ParaResponse(tarefa, branch))
            .ToList();

        return new TarefaCicloResponse(
            tarefa.Identifier,
            tarefa.Status,
            tarefa.StatusAnterior,
            tarefa.EmAndamento,
            tarefa.PrazoInterno,
            tarefa.PrazoInternoPendente,
            tarefa.PrazoAsana,
            tarefa.Prioridade,
            tarefa.ConcluidaEm,
            branches);
    }

    public static BranchResponse ParaResponse(Tarefa tarefa, TarefaBranch branch)
    {
        var retrabalho = branch.RetrabalhoId is { } retrabalhoId
            ? tarefa.Retrabalhos.FirstOrDefault(r => r.Id == retrabalhoId)
            : null;

        return new BranchResponse(branch.Identifier, branch.Tipo, branch.Nome, branch.Enviada, branch.EnviadaAsanaEm, retrabalho?.Numero);
    }
}
