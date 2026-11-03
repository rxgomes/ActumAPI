using Actum.Modules.Asana.Application;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Errors;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Synchronization of the local task copy with the Asana (design of <c>sincronizacao-de-tarefas</c>, sections 3 to 5)
/// </summary>
internal sealed class SincronizacaoService(
    TarefasDbContext context,
    IAsanaClient asana,
    SincronizacaoEmAndamento emAndamento,
    ILogger<SincronizacaoService> logger) : ISincronizacaoService
{
    private const string ErroInesperado = "Ocorreu um erro inesperado durante a sincronização.";

    public async Task<SincronizacaoResultado> SincronizarAsync(CancellationToken cancellationToken)
    {
        if (!emAndamento.TentarIniciar())
        {
            return SincronizacaoResultado.EmAndamento;
        }

        try
        {
            return SincronizacaoResultado.Concluida(await ExecutarAsync(cancellationToken));
        }
        finally
        {
            emAndamento.Finalizar();
        }
    }

    public async Task<SincronizacaoResponse?> ObterUltimaAsync(CancellationToken cancellationToken)
    {
        var ultima = await context.Set<Sincronizacao>()
            .AsNoTracking()
            .OrderByDescending(sincronizacao => sincronizacao.IniciadaEm)
            .ThenByDescending(sincronizacao => sincronizacao.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return ultima is null ? null : ParaResponse(ultima);
    }

    private async Task<SincronizacaoResponse> ExecutarAsync(CancellationToken cancellationToken)
    {
        var registro = new Sincronizacao(DateTime.Now);
        context.Add(registro);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var usuario = await asana.ObterUsuarioAtualAsync(cancellationToken);
            var empresas = await AtualizarEmpresasAsync(usuario, cancellationToken);

            foreach (var empresa in empresas)
            {
                await SincronizarEmpresaAsync(empresa, usuario, registro, cancellationToken);
            }

            registro.Concluir(DateTime.Now);
            await context.SaveChangesAsync(cancellationToken);

            return ParaResponse(registro);
        }
        catch (Exception ex)
        {
            await RegistrarFalhaAsync(registro.Id, ex);
            throw;
        }
    }

    /// <summary>
    /// Create a company for each new workspace and keep the names equal to the Asana. Returns the companies to
    /// synchronize: turned on and still accessible by the user.
    /// </summary>
    private async Task<List<Empresa>> AtualizarEmpresasAsync(AsanaUsuario usuario, CancellationToken cancellationToken)
    {
        var empresas = await context.Set<Empresa>().OrderBy(empresa => empresa.Id).ToListAsync(cancellationToken);

        foreach (var workspace in usuario.Workspaces)
        {
            var nome = workspace.Nome.Length <= Empresa.NomeMaxLength ? workspace.Nome : workspace.Nome[..Empresa.NomeMaxLength];
            var empresa = empresas.FirstOrDefault(e => e.AsanaWorkspaceGid == workspace.Gid);
            if (empresa is null)
            {
                empresa = new Empresa(workspace.Gid, nome, Empresa.Paleta[empresas.Count % Empresa.Paleta.Count]);
                context.Add(empresa);
                empresas.Add(empresa);
            }
            else if (empresa.Nome != nome)
            {
                empresa.Nome = nome;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        // Um workspace que o usuário não acessa mais não é consultado: toda chamada a ele falharia.
        var acessiveis = usuario.Workspaces.Select(workspace => workspace.Gid).ToHashSet();

        return empresas
            .Where(empresa => empresa.Sincronizar && acessiveis.Contains(empresa.AsanaWorkspaceGid))
            .ToList();
    }

    /// <summary>
    /// Synchronize one company and save it in a single <c>SaveChanges</c> (one transaction), together with the
    /// totals of the run
    /// </summary>
    private async Task SincronizarEmpresaAsync(
        Empresa empresa,
        AsanaUsuario usuario,
        Sincronizacao registro,
        CancellationToken cancellationToken)
    {
        var agora = DateTime.Now;
        var cargaInicial = empresa.CargaInicialPendente;
        var totais = new Totais();

        var abertas = (await asana.ListarTarefasAbertasAsync(empresa.AsanaWorkspaceGid, cancellationToken))
            .Where(tarefa => tarefa.ResponsavelGid is null || tarefa.ResponsavelGid == usuario.Gid)
            .ToList();

        var locais = await context.Set<Tarefa>()
            .Where(tarefa => tarefa.EmpresaId == empresa.Id)
            .Include(tarefa => tarefa.Comentarios)
            .Include(tarefa => tarefa.Retrabalhos)
            .AsSplitQuery()
            .ToDictionaryAsync(tarefa => tarefa.AsanaGid, cancellationToken);

        foreach (var remota in abertas)
        {
            if (locais.TryGetValue(remota.Gid, out var local))
            {
                await AtualizarTarefaAsync(local, remota, agora, totais, cancellationToken);
            }
            else
            {
                await CriarTarefaAsync(empresa, remota, cargaInicial, agora, cancellationToken);
                totais.Novas++;
            }
        }

        var gidsAbertos = abertas.Select(tarefa => tarefa.Gid).ToHashSet();
        var forDaLista = locais.Values
            .Where(tarefa => !gidsAbertos.Contains(tarefa.AsanaGid))
            // Concluídas aguardam teste: não são consultadas uma a uma, para economizar chamadas.
            .Where(tarefa => tarefa.Status is not (StatusTarefa.Concluida or StatusTarefa.Arquivada));

        foreach (var local in forDaLista)
        {
            if (await MotivoParaArquivarAsync(local, usuario, cancellationToken) is { } motivo)
            {
                local.Arquivar(motivo, agora);
                totais.Arquivadas++;
            }
        }

        if (cargaInicial)
        {
            empresa.ConcluirCargaInicial(agora);
        }

        registro.TarefasNovas += totais.Novas;
        registro.TarefasAtualizadas += totais.Atualizadas;
        registro.TarefasArquivadas += totais.Arquivadas;
        registro.Reaberturas += totais.Reaberturas;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Empresa {Empresa} sincronizada: {Novas} novas, {Atualizadas} atualizadas, {Arquivadas} arquivadas, {Reaberturas} reaberturas",
            empresa.Nome, totais.Novas, totais.Atualizadas, totais.Arquivadas, totais.Reaberturas);
    }

    private async Task CriarTarefaAsync(
        Empresa empresa,
        AsanaTarefa remota,
        bool cargaInicial,
        DateTime agora,
        CancellationToken cancellationToken)
    {
        var tarefa = Tarefa.Criar(empresa, remota, cargaInicial, agora);

        // Comentários anteriores à chegada da tarefa no Actum não são novidade para o usuário.
        var stories = await asana.ListarStoriesAsync(remota.Gid, cancellationToken);
        tarefa.AdicionarComentarios(stories, lidos: true);

        context.Add(tarefa);
    }

    private async Task AtualizarTarefaAsync(
        Tarefa local,
        AsanaTarefa remota,
        DateTime agora,
        Totais totais,
        CancellationToken cancellationToken)
    {
        var modificada = local.ModificadaNoAsana(remota);
        var statusAntes = local.Status;
        local.AtualizarDoAsana(remota, agora);

        // O histórico só é lido quando pode trazer novidade: tarefa modificada ou que voltou à lista.
        var voltouParaALista = statusAntes is StatusTarefa.Concluida or StatusTarefa.Arquivada;
        if (!modificada && !voltouParaALista)
        {
            return;
        }

        var stories = await asana.ListarStoriesAsync(remota.Gid, cancellationToken);
        local.AdicionarComentarios(stories, lidos: false);

        if (statusAntes == StatusTarefa.Concluida)
        {
            totais.Reaberturas += local.RegistrarReaberturas(stories, local.ConcluidaEm, registrarSemHistorico: true, agora).Count;
        }
        else if (statusAntes == StatusTarefa.Arquivada)
        {
            if (local.MotivoArquivamento == MotivoArquivamento.ConcluidaNoAsana)
            {
                totais.Reaberturas += local.RegistrarReaberturas(stories, local.ArquivadaEm, registrarSemHistorico: false, agora).Count;
            }

            if (local.Status == StatusTarefa.Arquivada)
            {
                local.Desarquivar();
            }
        }

        totais.Atualizadas++;
    }

    /// <summary>
    /// Why a task that left the open list must be archived, or <see langword="null"/> when it is still open and
    /// assigned to the user (it will be compared again in the next synchronization)
    /// </summary>
    private async Task<MotivoArquivamento?> MotivoParaArquivarAsync(Tarefa local, AsanaUsuario usuario, CancellationToken cancellationToken)
    {
        var remota = await asana.ObterTarefaAsync(local.AsanaGid, cancellationToken);
        if (remota is null)
        {
            return MotivoArquivamento.Excluida;
        }

        if (remota.Concluida)
        {
            return MotivoArquivamento.ConcluidaNoAsana;
        }

        if (remota.ResponsavelGid != usuario.Gid)
        {
            return MotivoArquivamento.Reatribuida;
        }

        return null;
    }

    /// <summary>
    /// Discard the unsaved changes of the company that failed and record the run as failed. The companies saved
    /// before stay in the database.
    /// </summary>
    private async Task RegistrarFalhaAsync(long sincronizacaoId, Exception ex)
    {
        var erro = ex switch
        {
            AsanaException asanaException => asanaException.Message,
            OperationCanceledException => "A sincronização foi cancelada.",
            _ => ErroInesperado,
        };

        try
        {
            context.ChangeTracker.Clear();
            var registro = await context.Set<Sincronizacao>().SingleAsync(s => s.Id == sincronizacaoId, CancellationToken.None);
            registro.Falhar(DateTime.Now, erro);
            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception falhaAoRegistrar)
        {
            logger.LogError(falhaAoRegistrar, "Não foi possível registrar a falha da sincronização {Id}", sincronizacaoId);
        }
    }

    private static SincronizacaoResponse ParaResponse(Sincronizacao sincronizacao)
    {
        return new SincronizacaoResponse(
            sincronizacao.Identifier,
            sincronizacao.IniciadaEm,
            sincronizacao.FinalizadaEm,
            sincronizacao.Sucesso,
            sincronizacao.TarefasNovas,
            sincronizacao.TarefasAtualizadas,
            sincronizacao.TarefasArquivadas,
            sincronizacao.Reaberturas,
            sincronizacao.Erro);
    }

    private sealed class Totais
    {
        public int Novas { get; set; }

        public int Atualizadas { get; set; }

        public int Arquivadas { get; set; }

        public int Reaberturas { get; set; }
    }
}
