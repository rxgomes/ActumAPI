using Actum.Modules.Asana.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Completion of a task (design of <c>ciclo-de-vida-da-tarefa</c>, section 5)
/// </summary>
internal sealed class ConclusaoService(
    TarefasDbContext context,
    IAsanaClient asana,
    ILogger<ConclusaoService> logger) : IConclusaoService
{
    private const string TarefaExcluidaNoAsana =
        "A tarefa não existe mais no Asana. Sincronize as tarefas para atualizar a lista.";

    public async Task<ConclusaoPreviaResponse?> ObterPreviaAsync(Guid identifier, CancellationToken cancellationToken)
    {
        var tarefa = await TarefaCiclo.CarregarAsync(context, identifier, cancellationToken);
        if (tarefa is null)
        {
            return null;
        }

        var motivo = tarefa.MotivoParaNaoConcluir();
        var tags = tarefa.BranchesPendentes.Select(branch => branch.Tag).ToList();

        return new ConclusaoPreviaResponse(motivo is null, motivo, tags);
    }

    public async Task<TarefaResultado> ConcluirAsync(Guid identifier, CancellationToken cancellationToken)
    {
        var tarefa = await TarefaCiclo.CarregarAsync(context, identifier, cancellationToken);
        if (tarefa is null)
        {
            return TarefaResultado.NaoEncontrada;
        }

        // Validado antes de qualquer chamada ao Asana.
        if (tarefa.MotivoParaNaoConcluir() is { } motivo)
        {
            throw new DomainException(motivo);
        }

        var remota = await asana.ObterTarefaAsync(tarefa.AsanaGid, cancellationToken)
            ?? throw new DomainException(TarefaExcluidaNoAsana);

        var workspaceGid = await context.Set<Empresa>()
            .Where(empresa => empresa.Id == tarefa.EmpresaId)
            .Select(empresa => empresa.AsanaWorkspaceGid)
            .SingleAsync(cancellationToken);

        foreach (var branch in tarefa.BranchesPendentes)
        {
            var tagNaTarefa = remota.Tags.FirstOrDefault(tag => string.Equals(tag.Nome, branch.Tag, StringComparison.Ordinal));
            if (tagNaTarefa is not null)
            {
                branch.MarcarEnviada(tagNaTarefa.Gid, DateTime.Now);
            }
            else
            {
                var tag = await asana.ObterOuCriarTagAsync(workspaceGid, branch.Tag, cancellationToken);
                await asana.AdicionarTagAsync(tarefa.AsanaGid, tag.Gid, cancellationToken);
                branch.MarcarEnviada(tag.Gid, DateTime.Now);
            }

            // Gravado a cada tag: uma nova tentativa após falha continua de onde parou, sem duplicar.
            await context.SaveChangesAsync(cancellationToken);
        }

        await asana.ConcluirTarefaAsync(tarefa.AsanaGid, cancellationToken);

        tarefa.MarcarConcluida(DateTime.Now);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tarefa {Tarefa} concluída no Asana e no Actum", tarefa.AsanaGid);

        return TarefaResultado.Sucesso(TarefaCiclo.ParaResponse(tarefa));
    }
}
