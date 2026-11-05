using Actum.Modules.Asana.Contracts;
using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Local copy of an Asana task assigned to the user, with the internal management data (status, internal due
/// date, priority, rework) that exists only in the Actum
/// </summary>
/// <remarks>
/// The fields that come from the Asana are read-only in the Actum and are overwritten by
/// <see cref="AtualizarDoAsana"/>. The internal fields are never sent to the Asana.
/// </remarks>
public sealed class Tarefa : Entity
{
    public const int GidMaxLength = 50;

    public const int TituloMaxLength = 1024;

    public const int ProjetosMaxLength = 1000;

    public const int LinkMaxLength = 500;

    public const int StatusMaxLength = 30;

    private const string SeparadorDeProjetos = ", ";

    private readonly List<Comentario> _comentarios = [];

    private readonly List<Retrabalho> _retrabalhos = [];

    private readonly List<TarefaBranch> _branches = [];

    private readonly List<Conclusao> _conclusoes = [];

    private Tarefa()
    {
    }

    /// <summary>
    /// Company (workspace) of the task
    /// </summary>
    public long EmpresaId { get; private set; }

    /// <summary>
    /// Task identifier in the Asana (unique)
    /// </summary>
    public string AsanaGid { get; private set; } = string.Empty;

    /// <summary>
    /// Task name in the Asana
    /// </summary>
    public string Titulo { get; private set; } = string.Empty;

    /// <summary>
    /// Description in plain text (<c>notes</c>)
    /// </summary>
    public string Descricao { get; private set; } = string.Empty;

    /// <summary>
    /// Description in HTML (<c>html_notes</c>)
    /// </summary>
    public string DescricaoHtml { get; private set; } = string.Empty;

    /// <summary>
    /// Names of the Asana projects, separated by <c>", "</c>
    /// </summary>
    public string Projetos { get; private set; } = string.Empty;

    /// <summary>
    /// Task URL in the Asana
    /// </summary>
    public string LinkAsana { get; private set; } = string.Empty;

    /// <summary>
    /// Due date in the Asana
    /// </summary>
    public DateOnly? PrazoAsana { get; private set; }

    /// <summary>
    /// Due date set by the user in the Actum
    /// </summary>
    public DateOnly? PrazoInterno { get; private set; }

    /// <summary>
    /// Whether the task was reopened and is waiting for a new internal due date
    /// </summary>
    public bool PrazoInternoPendente { get; private set; }

    /// <summary>
    /// Internal status
    /// </summary>
    public StatusTarefa Status { get; private set; }

    /// <summary>
    /// Status before blocking or archiving
    /// </summary>
    public StatusTarefa? StatusAnterior { get; private set; }

    /// <summary>
    /// Whether the user is working on the task now (<c>Em andamento</c>); otherwise it is paused (<c>Pausada</c>).
    /// Independent of <see cref="Status"/>: a task in rework can also be in progress or paused. Only
    /// <see cref="Iniciar"/> and <see cref="Retomar"/> turn it on; blocking, completing and archiving turn it off.
    /// </summary>
    public bool EmAndamento { get; private set; }

    /// <summary>
    /// Priority from 1 to 3; <see langword="null"/> when not set
    /// </summary>
    public short? Prioridade { get; private set; }

    /// <summary>
    /// Local date and time of the last change in the Asana, as of the last synchronization
    /// </summary>
    public DateTime AsanaModificadaEm { get; private set; }

    /// <summary>
    /// Local date and time of the last completion made by the Actum
    /// </summary>
    public DateTime? ConcluidaEm { get; private set; }

    /// <summary>
    /// Local date and time when the task was archived
    /// </summary>
    public DateTime? ArquivadaEm { get; private set; }

    /// <summary>
    /// Why the task was archived
    /// </summary>
    public MotivoArquivamento? MotivoArquivamento { get; private set; }

    /// <summary>
    /// Local date and time of the last synchronization that saw the task in the Asana
    /// </summary>
    public DateTime SincronizadaEm { get; private set; }

    /// <summary>
    /// Comments copied from the Asana
    /// </summary>
    public IReadOnlyCollection<Comentario> Comentarios => _comentarios;

    /// <summary>
    /// Reworks (reopenings), in order of <see cref="Retrabalho.Numero"/>
    /// </summary>
    public IReadOnlyCollection<Retrabalho> Retrabalhos => _retrabalhos;

    /// <summary>
    /// Git branches of the task
    /// </summary>
    public IReadOnlyCollection<TarefaBranch> Branches => _branches;

    /// <summary>
    /// Completions found in the Asana history
    /// </summary>
    public IReadOnlyCollection<Conclusao> Conclusoes => _conclusoes;

    /// <summary>
    /// Branches whose tags are not yet in the Asana, in the order they are sent: base, implementation, then creation
    /// </summary>
    public IReadOnlyList<TarefaBranch> BranchesPendentes => _branches
        .Where(branch => !branch.Enviada)
        .OrderBy(branch => branch.Tipo)
        .ThenBy(branch => branch.Id == 0 ? long.MaxValue : branch.Id)
        .ToList();

    /// <summary>
    /// Create the local copy of an Asana task. In the initial load of the company the task starts
    /// <see cref="StatusTarefa.EmAndamento"/> with the internal due date equal to the Asana due date; otherwise it
    /// starts in <see cref="StatusTarefa.Entrada"/> without internal due date.
    /// </summary>
    /// <param name="empresa">Company of the task</param>
    /// <param name="asana">Task in the Asana</param>
    /// <param name="cargaInicial">Whether this is the initial load of the company</param>
    /// <param name="agora">Local date and time of the synchronization</param>
    /// <returns>New task</returns>
    public static Tarefa Criar(Empresa empresa, AsanaTarefa asana, bool cargaInicial, DateTime agora)
    {
        var tarefa = new Tarefa
        {
            EmpresaId = empresa.Id,
            AsanaGid = asana.Gid,
            Status = cargaInicial ? StatusTarefa.EmAndamento : StatusTarefa.Entrada,
            PrazoInterno = cargaInicial ? asana.PrazoEm : null,
        };
        tarefa.AtualizarDoAsana(asana, agora);

        return tarefa;
    }

    /// <summary>
    /// Whether the task changed in the Asana since the last synchronization
    /// </summary>
    /// <param name="asana">Task in the Asana</param>
    /// <returns><see langword="true"/> when the Asana modification date is later than the stored one</returns>
    public bool ModificadaNoAsana(AsanaTarefa asana)
    {
        return asana.ModificadaEm > AsanaModificadaEm;
    }

    /// <summary>
    /// Overwrite the fields that come from the Asana. Internal fields (status, internal due date, priority) are kept.
    /// </summary>
    /// <param name="asana">Task in the Asana</param>
    /// <param name="agora">Local date and time of the synchronization</param>
    public void AtualizarDoAsana(AsanaTarefa asana, DateTime agora)
    {
        Titulo = Truncar(asana.Nome, TituloMaxLength);
        Descricao = asana.Notas;
        DescricaoHtml = asana.NotasHtml;
        Projetos = Truncar(string.Join(SeparadorDeProjetos, asana.Projetos), ProjetosMaxLength);
        LinkAsana = Truncar(asana.Link, LinkMaxLength);
        PrazoAsana = asana.PrazoEm;
        AsanaModificadaEm = asana.ModificadaEm;
        SincronizadaEm = agora;
    }

    /// <summary>
    /// Add the comments of the Asana history not yet copied
    /// </summary>
    /// <param name="stories">Task history in the Asana</param>
    /// <param name="lidos">Whether the new comments are marked as read</param>
    /// <returns>Number of comments added</returns>
    public int AdicionarComentarios(IEnumerable<AsanaStory> stories, bool lidos)
    {
        var novos = stories
            .Where(story => story.Tipo == AsanaStoryTipo.Comentario)
            .Where(story => _comentarios.All(comentario => comentario.AsanaStoryGid != story.Gid))
            .Select(story => new Comentario(story.Gid, story.AutorNome, story.Texto, story.CriadaEm, lidos))
            .ToList();

        _comentarios.AddRange(novos);

        return novos.Count;
    }

    /// <summary>
    /// Recalculate the completions and reworks from the Asana history: each reopening preceded by a completion is a
    /// rework, numbered by reopening date. Reworks already recorded for the same reopening are kept (with the due
    /// dates and the resolution recorded by the Actum); recorded reworks without a pair are removed and their
    /// branches lose only the link. Only a new pair reopened after the completion the Actum knows (completed task, or
    /// archived because it was completed in the Asana) puts the task in <see cref="StatusTarefa.Retrabalho"/> with
    /// the internal due date pending; older pairs are history. An open task whose last rework is still open (not completed
    /// again) is shown as in rework: <see cref="StatusTarefa.EmAndamento"/> becomes <see cref="StatusTarefa.Retrabalho"/>
    /// (and a blocked one returns to it), without changing the internal due date. A completed task back in the open list without such a
    /// pair goes back to <see cref="StatusTarefa.EmAndamento"/> with the internal due date pending.
    /// </summary>
    /// <param name="stories">Task history in the Asana</param>
    /// <param name="voltouParaALista">Whether the task is back in the open list of the user</param>
    /// <returns>New reworks that reopened the task (the history-only ones are not counted)</returns>
    public int RecalcularRetrabalhos(IEnumerable<AsanaStory> stories, bool voltouParaALista)
    {
        var historico = HistoricoDeConclusoes.Calcular(stories);

        foreach (var conclusao in historico.Conclusoes.Where(conclusao => _conclusoes.All(existente => existente.AsanaStoryGid != conclusao.StoryGid)))
        {
            _conclusoes.Add(new Conclusao(conclusao.StoryGid, conclusao.Em, conclusao.Por));
        }

        DateTime? referencia = Status switch
        {
            StatusTarefa.Concluida => ConcluidaEm,
            StatusTarefa.Arquivada when MotivoArquivamento == Domain.MotivoArquivamento.ConcluidaNoAsana => ArquivadaEm,
            _ => null,
        };

        var mantidos = new List<Retrabalho>();
        var reabriram = 0;
        foreach (var par in historico.Pares)
        {
            var existente = _retrabalhos.FirstOrDefault(retrabalho => retrabalho.AsanaStoryGid == par.ReaberturaGid);
            if (existente is not null)
            {
                mantidos.Add(existente);
                continue;
            }

            var reabriu = referencia is not null && par.ReabertaEm > referencia;
            var novo = new Retrabalho(0, par.ReabertaEm, par.ReabertaPor, par.ReaberturaGid, reabriu ? PrazoInterno : null, par.ConcluidaEm)
            {
                ResolvidoEm = par.ResolvidoEm,
            };
            _retrabalhos.Add(novo);
            mantidos.Add(novo);

            if (reabriu)
            {
                reabriram++;
            }
        }

        foreach (var semPar in _retrabalhos.Except(mantidos).ToList())
        {
            foreach (var branch in _branches.Where(branch => branch.RetrabalhoId is not null && branch.RetrabalhoId == semPar.Id))
            {
                branch.DesvincularRetrabalho();
            }

            _retrabalhos.Remove(semPar);
        }

        // Numeração pela data da reabertura, com os pares na mesma ordem do histórico.
        for (var indice = 0; indice < historico.Pares.Count; indice++)
        {
            var par = historico.Pares[indice];
            _retrabalhos.Single(retrabalho => retrabalho.AsanaStoryGid == par.ReaberturaGid).AtualizarDoHistorico((short)(indice + 1), par);
        }

        if (reabriram > 0)
        {
            Status = StatusTarefa.Retrabalho;
            StatusAnterior = null;
            PrazoInternoPendente = true;
            LimparArquivamento();
        }
        else if (voltouParaALista && Status == StatusTarefa.Concluida)
        {
            Status = StatusTarefa.EmAndamento;
            StatusAnterior = null;
            PrazoInternoPendente = true;
        }

        // Retrabalho em aberto (o último par não foi concluído de novo): a tarefa em andamento passa a "retrabalho",
        // sem mudar o prazo; a bloqueada volta para "retrabalho" ao desbloquear. A entrada fica como está.
        var ultimo = _retrabalhos.MaxBy(retrabalho => retrabalho.Numero);
        if (ultimo is { ResolvidoEm: null })
        {
            if (Status == StatusTarefa.EmAndamento)
            {
                Status = StatusTarefa.Retrabalho;
            }
            else if (Status == StatusTarefa.Bloqueada && StatusAnterior == StatusTarefa.EmAndamento)
            {
                StatusAnterior = StatusTarefa.Retrabalho;
            }
        }

        return reabriram;
    }

    /// <summary>
    /// Archive the task because it left the open list of the user in the Asana
    /// </summary>
    /// <param name="motivo">Reason</param>
    /// <param name="agora">Local date and time of the synchronization</param>
    public void Arquivar(MotivoArquivamento motivo, DateTime agora)
    {
        StatusAnterior = Status;
        Status = StatusTarefa.Arquivada;
        EmAndamento = false;
        ArquivadaEm = agora;
        MotivoArquivamento = motivo;
    }

    /// <summary>
    /// Bring an archived task back to the inbox, because it is again assigned to the user and open in the Asana
    /// </summary>
    public void Desarquivar()
    {
        Status = StatusTarefa.Entrada;
        StatusAnterior = null;
        LimparArquivamento();
    }

    /// <summary>
    /// Start a task from the inbox with its base and implementation branches and the internal due date. Nothing
    /// is sent to the Asana.
    /// </summary>
    /// <param name="branchBase">Base branch name</param>
    /// <param name="branchImplementacao">Implementation branch name</param>
    /// <param name="prazoInterno">Internal due date</param>
    /// <exception cref="DomainException">The task is not in the inbox, or the branches are empty or equal</exception>
    public void Iniciar(string branchBase, string branchImplementacao, DateOnly prazoInterno)
    {
        if (Status != StatusTarefa.Entrada)
        {
            throw new DomainException($"Só é possível iniciar uma tarefa que está na entrada. Status atual: {StatusTarefaTexto.Descrever(Status)}.");
        }

        var nomeBase = NomeDaBranch(branchBase);
        var nomeImplementacao = NomeDaBranch(branchImplementacao);
        if (string.Equals(nomeBase, nomeImplementacao, StringComparison.Ordinal))
        {
            throw new DomainException("A branch base e a branch de implementação devem ser diferentes.");
        }

        Status = StatusTarefa.EmAndamento;
        EmAndamento = true;
        DefinirPrazoInterno(prazoInterno);
        _branches.Add(new TarefaBranch(TipoBranch.Base, nomeBase, null));
        _branches.Add(new TarefaBranch(TipoBranch.Implementacao, nomeImplementacao, null));
    }

    /// <summary>
    /// Block a task in progress or in rework (waiting for someone); the current status is kept to return to it
    /// </summary>
    /// <exception cref="DomainException">The task is not in progress nor in rework</exception>
    public void Bloquear()
    {
        if (Status is not (StatusTarefa.EmAndamento or StatusTarefa.Retrabalho))
        {
            throw new DomainException($"Só é possível bloquear tarefas em andamento ou em retrabalho. Status atual: {StatusTarefaTexto.Descrever(Status)}.");
        }

        StatusAnterior = Status;
        Status = StatusTarefa.Bloqueada;
        EmAndamento = false;
    }

    /// <summary>
    /// Resume work on a paused task in progress or in rework (<c>Em andamento</c>). Nothing is sent to the Asana.
    /// </summary>
    /// <exception cref="DomainException">The task is not in progress nor in rework, or is already in progress</exception>
    public void Retomar()
    {
        if (Status == StatusTarefa.Bloqueada)
        {
            throw new DomainException("Desbloqueie a tarefa antes de iniciá-la.");
        }

        if (Status == StatusTarefa.Entrada)
        {
            throw new DomainException("Para iniciar uma tarefa da entrada, informe as branches e o prazo interno.");
        }

        if (Status is not (StatusTarefa.EmAndamento or StatusTarefa.Retrabalho))
        {
            throw new DomainException($"Só é possível iniciar tarefas pausadas ou em retrabalho. Status atual: {StatusTarefaTexto.Descrever(Status)}.");
        }

        if (EmAndamento)
        {
            throw new DomainException("A tarefa já está em andamento.");
        }

        EmAndamento = true;
    }

    /// <summary>
    /// Pause a task in progress (<c>Pausada</c>). Nothing is sent to the Asana.
    /// </summary>
    /// <exception cref="DomainException">The task is not in progress</exception>
    public void Pausar()
    {
        if (!EmAndamento)
        {
            throw new DomainException("Só é possível pausar uma tarefa em andamento.");
        }

        EmAndamento = false;
    }

    /// <summary>
    /// Unblock a task, returning it to the status it had before blocking
    /// </summary>
    /// <exception cref="DomainException">The task is not blocked</exception>
    public void Desbloquear()
    {
        if (Status != StatusTarefa.Bloqueada)
        {
            throw new DomainException($"Só é possível desbloquear tarefas bloqueadas. Status atual: {StatusTarefaTexto.Descrever(Status)}.");
        }

        Status = StatusAnterior ?? StatusTarefa.EmAndamento;
        StatusAnterior = null;
    }

    /// <summary>
    /// Set the internal due date. When the date is pending after a reopening, it is also recorded as the new due
    /// date of the open reworks and the pending flag is cleared.
    /// </summary>
    /// <param name="prazo">Internal due date</param>
    /// <exception cref="DomainException">The task is archived</exception>
    public void DefinirPrazoInterno(DateOnly prazo)
    {
        GarantirNaoArquivada();

        PrazoInterno = prazo;
        if (PrazoInternoPendente)
        {
            foreach (var retrabalho in RetrabalhosAbertos())
            {
                retrabalho.PrazoInternoNovo = prazo;
            }

            PrazoInternoPendente = false;
        }
    }

    /// <summary>
    /// Set the priority, from 1 (highest) to 3
    /// </summary>
    /// <param name="prioridade">Priority from 1 to 3</param>
    /// <exception cref="DomainException">The priority is out of range or the task is archived</exception>
    public void DefinirPrioridade(short prioridade)
    {
        GarantirNaoArquivada();

        if (prioridade is < 1 or > 3)
        {
            throw new DomainException("A prioridade deve estar entre 1 e 3.");
        }

        Prioridade = prioridade;
    }

    /// <summary>
    /// Register a branch, linked to the open rework when there is one
    /// </summary>
    /// <param name="tipo">Role of the branch</param>
    /// <param name="nome">Branch name (spaces at the ends are removed)</param>
    /// <returns>The new branch, or <see langword="null"/> when the task already has a branch with this name</returns>
    /// <exception cref="DomainException">The task is not in progress, blocked or in rework, or the name is empty</exception>
    public TarefaBranch? AdicionarBranch(TipoBranch tipo, string nome)
    {
        if (Status is not (StatusTarefa.EmAndamento or StatusTarefa.Bloqueada or StatusTarefa.Retrabalho))
        {
            throw new DomainException($"Só é possível registrar branches em tarefas em andamento, bloqueadas ou em retrabalho. Status atual: {StatusTarefaTexto.Descrever(Status)}.");
        }

        var nomeDaBranch = NomeDaBranch(nome);
        if (_branches.Any(branch => string.Equals(branch.Nome, nomeDaBranch, StringComparison.Ordinal)))
        {
            return null;
        }

        var retrabalhoAberto = RetrabalhosAbertos().MaxBy(retrabalho => retrabalho.Numero);
        var nova = new TarefaBranch(tipo, nomeDaBranch, retrabalhoAberto?.Id);
        _branches.Add(nova);

        return nova;
    }

    /// <summary>
    /// Remove a branch not yet sent to the Asana (to fix a typo)
    /// </summary>
    /// <param name="identifier">Branch identifier</param>
    /// <returns><see langword="false"/> when the task has no branch with this identifier</returns>
    /// <exception cref="DomainException">The branch was already sent to the Asana</exception>
    public bool RemoverBranch(Guid identifier)
    {
        var branch = _branches.FirstOrDefault(b => b.Identifier == identifier);
        if (branch is null)
        {
            return false;
        }

        if (branch.Enviada)
        {
            throw new DomainException($"A branch '{branch.Nome}' já foi enviada ao Asana como tag e não pode ser excluída.");
        }

        _branches.Remove(branch);
        return true;
    }

    /// <summary>
    /// Why the task cannot be completed now, or <see langword="null"/> when it can: the status must be in progress,
    /// blocked or rework, and there must be at least one base and one implementation branch
    /// </summary>
    /// <returns>Reason in pt-BR, or <see langword="null"/></returns>
    public string? MotivoParaNaoConcluir()
    {
        if (Status is not (StatusTarefa.EmAndamento or StatusTarefa.Bloqueada or StatusTarefa.Retrabalho))
        {
            return $"Só é possível concluir tarefas em andamento, bloqueadas ou em retrabalho. Status atual: {StatusTarefaTexto.Descrever(Status)}.";
        }

        var temBase = _branches.Any(branch => branch.Tipo == TipoBranch.Base);
        var temImplementacao = _branches.Any(branch => branch.Tipo == TipoBranch.Implementacao);

        return (temBase, temImplementacao) switch
        {
            (false, false) => "Informe a branch base e a branch de implementação antes de concluir.",
            (false, true) => "Informe a branch base antes de concluir.",
            (true, false) => "Informe a branch de implementação antes de concluir.",
            _ => null,
        };
    }

    /// <summary>
    /// Mark the task as completed by the Actum (after the Asana completion) and resolve the open reworks
    /// </summary>
    /// <param name="agora">Local date and time of the completion</param>
    /// <exception cref="DomainException">The task cannot be completed (see <see cref="MotivoParaNaoConcluir"/>)</exception>
    public void MarcarConcluida(DateTime agora)
    {
        if (MotivoParaNaoConcluir() is { } motivo)
        {
            throw new DomainException(motivo);
        }

        foreach (var retrabalho in RetrabalhosAbertos())
        {
            retrabalho.ResolvidoEm = agora;
        }

        Status = StatusTarefa.Concluida;
        StatusAnterior = null;
        EmAndamento = false;
        ConcluidaEm = agora;
    }

    private IEnumerable<Retrabalho> RetrabalhosAbertos()
    {
        return _retrabalhos.Where(retrabalho => retrabalho.ResolvidoEm is null);
    }

    private void GarantirNaoArquivada()
    {
        if (Status == StatusTarefa.Arquivada)
        {
            throw new DomainException("Tarefas arquivadas não podem ser alteradas.");
        }
    }

    private static string NomeDaBranch(string nome)
    {
        var nomeDaBranch = nome.Trim();
        if (nomeDaBranch.Length == 0)
        {
            throw new DomainException("O nome da branch é obrigatório.");
        }

        if (nomeDaBranch.Length > TarefaBranch.NomeMaxLength)
        {
            throw new DomainException($"O nome da branch deve ter no máximo {TarefaBranch.NomeMaxLength} caracteres.");
        }

        return nomeDaBranch;
    }

    private void LimparArquivamento()
    {
        ArquivadaEm = null;
        MotivoArquivamento = null;
    }

    private static string Truncar(string valor, int tamanhoMaximo)
    {
        return valor.Length <= tamanhoMaximo ? valor : valor[..tamanhoMaximo];
    }
}
