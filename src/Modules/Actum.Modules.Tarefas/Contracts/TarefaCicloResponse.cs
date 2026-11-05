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
/// <param name="Nome">Branch name, also the tag name in the Asana</param>
/// <param name="Enviada">Whether the tag is already in the Asana task</param>
/// <param name="EnviadaAsanaEm">When the tag was added in the Asana</param>
/// <param name="RetrabalhoNumero">Number of the rework open when the branch was registered</param>
public sealed record BranchResponse(
    Guid Identifier,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<TipoBranch>))] TipoBranch Tipo,
    string Nome,
    bool Enviada,
    DateTime? EnviadaAsanaEm,
    short? RetrabalhoNumero);

/// <summary>
/// Preview of the completion of a task
/// </summary>
/// <param name="Permitida">Whether the task can be completed now</param>
/// <param name="Motivo">Why it cannot be completed, in pt-BR</param>
/// <param name="Tags">Tags that will be added to the Asana task (branches not yet sent), in sending order</param>
public sealed record ConclusaoPreviaResponse(bool Permitida, string? Motivo, IReadOnlyList<string> Tags);
