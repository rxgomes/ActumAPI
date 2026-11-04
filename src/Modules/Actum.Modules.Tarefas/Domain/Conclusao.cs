using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Completion of a task found in the Asana history ("concluiu esta tarefa"), used to compute the rework rate
/// </summary>
public sealed class Conclusao : Entity
{
    public const int GidMaxLength = 50;

    public const int ConcluidaPorMaxLength = 200;

    internal Conclusao(string asanaStoryGid, DateTime concluidaEm, string? concluidaPor)
    {
        AsanaStoryGid = asanaStoryGid;
        ConcluidaEm = concluidaEm;
        ConcluidaPor = concluidaPor is { Length: > ConcluidaPorMaxLength } ? concluidaPor[..ConcluidaPorMaxLength] : concluidaPor;
    }

    /// <summary>
    /// Completed task
    /// </summary>
    public long TarefaId { get; private set; }

    /// <summary>
    /// Story identifier in the Asana (unique)
    /// </summary>
    public string AsanaStoryGid { get; private set; }

    /// <summary>
    /// Local date and time of the completion
    /// </summary>
    public DateTime ConcluidaEm { get; private set; }

    /// <summary>
    /// Who completed the task; <see langword="null"/> when unknown
    /// </summary>
    public string? ConcluidaPor { get; private set; }
}
