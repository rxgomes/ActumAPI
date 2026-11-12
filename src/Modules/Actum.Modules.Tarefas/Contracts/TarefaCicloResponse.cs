using System.Text.Json.Serialization;
using Actum.Modules.Tarefas.Domain;

namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Lifecycle state of a task, returned by the lifecycle actions
/// </summary>
/// <param name="Identifier">Task identifier</param>
/// <param name="Status">Internal status (<c>entrada</c>, <c>em_andamento</c>, <c>bloqueada</c>, <c>concluida</c>, <c>retrabalho</c>, <c>arquivada</c>)</param>
/// <param name="StatusAnterior">Status to return to when unblocked</param>
/// <param name="EmAndamento">Whether the task is in progress (<c>Em andamento</c>); otherwise it is paused (<c>Pausada</c>)</param>
/// <param name="PrazoInterno">Internal due date</param>
/// <param name="PrazoInternoPendente">Whether the task was reopened and waits for a new internal due date</param>
/// <param name="PrazoAsana">Due date in the Asana</param>
/// <param name="Prioridade">Priority from 1 to 3</param>
/// <param name="ConcluidaEm">Local date and time of the last completion by the Actum</param>
/// <param name="Branches">Branches of the task</param>
public sealed record TarefaCicloResponse(
    Guid Identifier,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<StatusTarefa>))] StatusTarefa Status,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<StatusTarefa>))] StatusTarefa? StatusAnterior,
    bool EmAndamento,
    DateOnly? PrazoInterno,
    bool PrazoInternoPendente,
    DateOnly? PrazoAsana,
    short? Prioridade,
    DateTime? ConcluidaEm,
    IReadOnlyList<BranchResponse> Branches);

/// <summary>
/// Git branch of a task
/// </summary>
/// <param name="Identifier">Branch identifier</param>
/// <param name="Tipo">Role: <c>base</c> or <c>implementacao</c></param>
/// <param name="Nome">Branch name</param>
/// <param name="Enviada">Whether the tag is already in the Asana task</param>
/// <param name="EnviadaAsanaEm">When the tag was added in the Asana</param>
/// <param name="RetrabalhoNumero">Number of the rework open when the branch was registered</param>
/// <param name="Partes">Parts of the branch (<c>front</c>, <c>back</c>), in this order; empty for branches registered before the parts existed</param>
/// <param name="Tag">Text of the tag in the Asana: <c>(Front, Back) => nome</c>, <c>(Front) => nome</c> or <c>(Back) => nome</c>; just the name when the branch has no parts</param>
/// <param name="Worktree">Local worktrees of the branch (implementation branches only); <see langword="null"/> when the branch never had them</param>
public sealed record BranchResponse(
    Guid Identifier,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<TipoBranch>))] TipoBranch Tipo,
    string Nome,
    bool Enviada,
    DateTime? EnviadaAsanaEm,
    short? RetrabalhoNumero,
    IReadOnlyList<ParteBranch> Partes,
    string Tag,
    WorktreeResponse? Worktree);

/// <summary>
/// Local worktrees of an implementation branch: one folder per task with one git worktree per part inside it
/// </summary>
/// <param name="Pasta">Folder of the task (<c>&lt;pastaWorktrees&gt;\&lt;branch&gt;</c>), with <c>front</c> and <c>back</c> inside it</param>
/// <param name="Situacao">Overall situation: <c>removido</c> when removed; otherwise <c>falhou</c> when a part is not created; otherwise <c>criado</c></param>
/// <param name="Partes">Worktree of each part, in the order front, back</param>
/// <param name="ComandoClaude">PowerShell command that opens the Claude Code in the folder of the task (e.g. <c>cd 'E:\wt\aureum\x'; claude</c>)</param>
public sealed record WorktreeResponse(
    string Pasta,
    SituacaoWorktree Situacao,
    IReadOnlyList<WorktreeParteResponse> Partes,
    string ComandoClaude);

/// <summary>
/// Git worktree of one part of an implementation branch
/// </summary>
/// <param name="Parte">Part (<c>front</c> or <c>back</c>)</param>
/// <param name="Caminho">Folder of the worktree</param>
/// <param name="Situacao">Situation (<c>criado</c>, <c>falhou</c> or <c>removido</c>)</param>
/// <param name="Erro">Error of the last attempt, in pt-BR; <see langword="null"/> when it succeeded</param>
/// <param name="CriadoEm">When the worktree was created</param>
public sealed record WorktreeParteResponse(
    ParteBranch Parte,
    string Caminho,
    SituacaoWorktree Situacao,
    string? Erro,
    DateTime? CriadoEm);

/// <summary>
/// Preview of the completion of a task
/// </summary>
/// <param name="Permitida">Whether the task can be completed now</param>
/// <param name="Motivo">Why it cannot be completed, in pt-BR</param>
/// <param name="Tags">Tags that will be added to the Asana task (branches not yet sent), in sending order</param>
public sealed record ConclusaoPreviaResponse(bool Permitida, string? Motivo, IReadOnlyList<string> Tags);
