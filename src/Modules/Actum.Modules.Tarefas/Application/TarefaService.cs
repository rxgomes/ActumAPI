using Actum.Modules.GitHub.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Task lifecycle actions over <see cref="TarefasDbContext"/>; the rules live in <see cref="Tarefa"/>. Starting a
/// task and registering a branch check or create the branches in the GitHub before saving: when the GitHub fails,
/// nothing is saved.
/// </summary>
internal sealed class TarefaService(TarefasDbContext context, IGitHubClient gitHub) : ITarefaService
{
    private readonly BranchesNoGitHub _branchesNoGitHub = new(gitHub);

    public async Task<TarefaResultado> IniciarAsync(Guid identifier, IniciarTarefaRequest request, CancellationToken cancellationToken)
    {
        var tarefa = await TarefaCiclo.CarregarAsync(context, identifier, cancellationToken);
        if (tarefa is null)
        {
            return TarefaResultado.NaoEncontrada;
        }

        // Regras de domínio antes do GitHub: status, nomes e partes inválidos não chegam a chamá-lo.
        var branches = tarefa.Iniciar(
            request.BranchBase!,
            request.BranchImplementacao!,
            request.PrazoInterno!.Value,
            request.Partes!);
        if (branches is not var (branchBase, branchImplementacao))
        {
            return TarefaResultado.BranchRepetida;
        }

        var empresa = await EmpresaDaTarefaAsync(tarefa, cancellationToken);
        await _branchesNoGitHub.CriarImplementacaoAsync(empresa, branchImplementacao.Partes, branchBase.Nome, branchImplementacao.Nome, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return TarefaResultado.Sucesso(TarefaCiclo.ParaResponse(tarefa));
    }

    public async Task<IReadOnlyList<string>?> ListarBasesRecentesAsync(Guid identifier, CancellationToken cancellationToken)
    {
        var empresaId = await context.Set<Tarefa>()
            .Where(tarefa => tarefa.Identifier == identifier)
            .Select(tarefa => (long?)tarefa.EmpresaId)
            .SingleOrDefaultAsync(cancellationToken);

        if (empresaId is null)
        {
            return null;
        }

        var nomes = await context.Set<Tarefa>()
            .Where(tarefa => tarefa.EmpresaId == empresaId)
            .SelectMany(tarefa => tarefa.Branches)
            .Where(branch => branch.Tipo == TipoBranch.Base)
            .OrderByDescending(branch => branch.CreateIn)
            .ThenByDescending(branch => branch.Id)
            .Select(branch => branch.Nome)
            .ToListAsync(cancellationToken);

        // Poucas linhas (um usuário): a distinção mantendo a ordem fica em memória.
        return nomes.Distinct(StringComparer.Ordinal).Take(QuantidadeDeBasesRecentes).ToList();
    }

    private const int QuantidadeDeBasesRecentes = 3;

    public async Task<TarefaResultado> RetomarAsync(Guid identifier, RetomarTarefaRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return await AplicarAsync(identifier, tarefa => tarefa.Retomar(), cancellationToken);
        }

        var tarefa = await TarefaCiclo.CarregarAsync(context, identifier, cancellationToken);
        if (tarefa is null)
        {
            return TarefaResultado.NaoEncontrada;
        }

        // Regras de domínio antes do GitHub, como no início.
        var branches = tarefa.RetomarComBranch(request.BranchBase!, request.BranchImplementacao!, request.Partes!);
        if (branches is not var (branchBase, branchImplementacao))
        {
            return TarefaResultado.BranchRepetida;
        }

        // As partes da implementação nova decidem os repositórios; a base reaproveitada pode ter outras.
        var empresa = await EmpresaDaTarefaAsync(tarefa, cancellationToken);
        await _branchesNoGitHub.CriarImplementacaoAsync(empresa, branchImplementacao.Partes, branchBase.Nome, branchImplementacao.Nome, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return TarefaResultado.Sucesso(TarefaCiclo.ParaResponse(tarefa));
    }

    public Task<TarefaResultado> PausarAsync(Guid identifier, CancellationToken cancellationToken)
    {
        return AplicarAsync(identifier, tarefa => tarefa.Pausar(), cancellationToken);
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

        // A base mais recente é lida antes de registrar a nova, que pode ser ela mesma uma base.
        var baseAtual = tarefa.BaseMaisRecente();

        var branch = tarefa.AdicionarBranch(request.Tipo!.Value, request.Nome!, request.Partes!);
        if (branch is null)
        {
            return TarefaResultado.BranchRepetida;
        }

        var empresa = await EmpresaDaTarefaAsync(tarefa, cancellationToken);
        if (branch.Tipo == TipoBranch.Base)
        {
            await _branchesNoGitHub.ConferirBaseAsync(empresa, branch.Partes, branch.Nome, cancellationToken);
        }
        else
        {
            // AdicionarBranch garante que há base para uma implementação.
            await _branchesNoGitHub.CriarImplementacaoAsync(empresa, branch.Partes, baseAtual!.Nome, branch.Nome, cancellationToken);
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

    private Task<Empresa> EmpresaDaTarefaAsync(Tarefa tarefa, CancellationToken cancellationToken)
    {
        return context.Set<Empresa>()
            .AsNoTracking()
            .SingleAsync(empresa => empresa.Id == tarefa.EmpresaId, cancellationToken);
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
