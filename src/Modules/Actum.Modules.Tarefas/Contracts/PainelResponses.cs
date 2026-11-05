using System.Text.Json.Serialization;
using Actum.Modules.Tarefas.Domain;

namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Board: open tasks grouped by what to do first, then the tasks completed by the Actum
/// </summary>
/// <param name="Hoje">Date used as "today" by the grouping</param>
/// <param name="Grupos">
/// Every group, always present and in this order: <c>retrabalho</c>, <c>entrada</c>, <c>atrasadas</c>, <c>hoje</c>,
/// <c>esta_semana</c>, <c>proximas</c>, <c>sem_prazo</c>, <c>concluidas</c> (completed by the Actum, most recent first)
/// </param>
public sealed record PainelResponse(DateOnly Hoje, IReadOnlyList<GrupoPainelResponse> Grupos);

/// <summary>
/// One board group
/// </summary>
/// <param name="Grupo">Group id (<c>retrabalho</c>, <c>entrada</c>, <c>atrasadas</c>, <c>hoje</c>, <c>esta_semana</c>, <c>proximas</c>, <c>sem_prazo</c>, <c>concluidas</c>)</param>
/// <param name="Tarefas">Tasks of the group, in display order; empty when there is none</param>
public sealed record GrupoPainelResponse(string Grupo, IReadOnlyList<CartaoTarefaResponse> Tarefas);

/// <summary>
/// Task card, used by the board and the list
/// </summary>
/// <param name="Identifier">Task identifier</param>
/// <param name="Titulo">Task name in the Asana</param>
/// <param name="Empresa">Company of the task</param>
/// <param name="Projetos">Names of the Asana projects, separated by <c>", "</c></param>
/// <param name="Status">Internal status</param>
/// <param name="PrazoInterno">Internal due date</param>
/// <param name="PrazoAsana">Due date in the Asana (shown by the front only when it differs from the internal one)</param>
/// <param name="PrazoInternoPendente">Whether the task was reopened and waits for a new internal due date</param>
/// <param name="Prioridade">Priority from 1 to 3</param>
/// <param name="QuantidadeRetrabalhos">Number of reworks (reopenings)</param>
/// <param name="ComentariosNaoLidos">Number of comments not yet read</param>
/// <param name="Bloqueada">Whether the task is blocked</param>
/// <param name="EmAndamento">Whether the task is in progress (<c>Em andamento</c>); otherwise it is paused (<c>Pausada</c>)</param>
public sealed record CartaoTarefaResponse(
    Guid Identifier,
    string Titulo,
    EmpresaResumoResponse Empresa,
    string Projetos,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<StatusTarefa>))] StatusTarefa Status,
    DateOnly? PrazoInterno,
    DateOnly? PrazoAsana,
    bool PrazoInternoPendente,
    short? Prioridade,
    int QuantidadeRetrabalhos,
    int ComentariosNaoLidos,
    bool Bloqueada,
    bool EmAndamento);

/// <summary>
/// Company data shown with a task
/// </summary>
/// <param name="Identifier">Company identifier</param>
/// <param name="Nome">Company name</param>
/// <param name="Cor">Company color (<c>#RRGGBB</c>)</param>
public sealed record EmpresaResumoResponse(Guid Identifier, string Nome, string Cor);

/// <summary>
/// Task detail: data, branches, reworks (each with the comments of its window) and comments
/// </summary>
/// <param name="Identifier">Task identifier</param>
/// <param name="Titulo">Task name in the Asana</param>
/// <param name="Descricao">Description in plain text</param>
/// <param name="DescricaoHtml">Description in HTML</param>
/// <param name="Projetos">Names of the Asana projects, separated by <c>", "</c></param>
/// <param name="LinkAsana">Task URL in the Asana</param>
/// <param name="Empresa">Company of the task</param>
/// <param name="Status">Internal status</param>
/// <param name="StatusAnterior">Status before blocking or archiving</param>
/// <param name="EmAndamento">Whether the task is in progress (<c>Em andamento</c>); otherwise it is paused (<c>Pausada</c>)</param>
/// <param name="PrazoInterno">Internal due date</param>
/// <param name="PrazoInternoPendente">Whether the task was reopened and waits for a new internal due date</param>
/// <param name="PrazoAsana">Due date in the Asana</param>
/// <param name="Prioridade">Priority from 1 to 3</param>
/// <param name="ConcluidaEm">Local date and time of the last completion by the Actum</param>
/// <param name="ArquivadaEm">Local date and time when the task was archived</param>
/// <param name="MotivoArquivamento">Why the task was archived (<c>concluida_no_asana</c>, <c>reatribuida</c>, <c>excluida</c>)</param>
/// <param name="SincronizadaEm">Local date and time of the last synchronization that saw the task</param>
/// <param name="Branches">Branches, indicating whether each was sent to the Asana</param>
/// <param name="Retrabalhos">Reworks in order of number</param>
/// <param name="Comentarios">Every comment, oldest first</param>
public sealed record TarefaDetalheResponse(
    Guid Identifier,
    string Titulo,
    string Descricao,
    string DescricaoHtml,
    string Projetos,
    string LinkAsana,
    EmpresaResumoResponse Empresa,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<StatusTarefa>))] StatusTarefa Status,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<StatusTarefa>))] StatusTarefa? StatusAnterior,
    bool EmAndamento,
    DateOnly? PrazoInterno,
    bool PrazoInternoPendente,
    DateOnly? PrazoAsana,
    short? Prioridade,
    DateTime? ConcluidaEm,
    DateTime? ArquivadaEm,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<MotivoArquivamento>))] MotivoArquivamento? MotivoArquivamento,
    DateTime SincronizadaEm,
    IReadOnlyList<BranchResponse> Branches,
    IReadOnlyList<RetrabalhoResponse> Retrabalhos,
    IReadOnlyList<ComentarioResponse> Comentarios);

/// <summary>
/// Rework (reopening) of a task
/// </summary>
/// <param name="Identifier">Rework identifier</param>
/// <param name="Numero">Sequence of the rework in the task, starting at 1</param>
/// <param name="ReabertaEm">Local date and time of the reopening</param>
/// <param name="ReabertaPor">Who reopened the task; <see langword="null"/> when not found in the Asana history</param>
/// <param name="PrazoInternoAnterior">Internal due date in force when the task was reopened</param>
/// <param name="PrazoInternoNovo">Internal due date set after the reopening</param>
/// <param name="ResolvidoEm">When the rework was completed</param>
/// <param name="ConcluidaEm">Completion that preceded the reopening (from the Asana history); <see langword="null"/> before the history is reprocessed</param>
/// <param name="Comentarios">
/// Comments made from the day of this reopening until the day before the next one (or until now), which usually
/// explain the reason
/// </param>
public sealed record RetrabalhoResponse(
    Guid Identifier,
    short Numero,
    DateTime ReabertaEm,
    string? ReabertaPor,
    DateOnly? PrazoInternoAnterior,
    DateOnly? PrazoInternoNovo,
    DateTime? ResolvidoEm,
    IReadOnlyList<ComentarioResponse> Comentarios,
    DateTime? ConcluidaEm = null);

/// <summary>
/// Comment of a task, copied from the Asana
/// </summary>
/// <param name="Identifier">Comment identifier</param>
/// <param name="Autor">Author name</param>
/// <param name="Texto">Comment text</param>
/// <param name="CriadoEm">Local date and time of the comment</param>
/// <param name="Lido">Whether the user already saw it</param>
public sealed record ComentarioResponse(Guid Identifier, string? Autor, string Texto, DateTime CriadoEm, bool Lido);
