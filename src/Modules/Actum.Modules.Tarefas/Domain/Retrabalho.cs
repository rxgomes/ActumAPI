using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// One reopening of a completed task (rework), numbered per task in chronological order
/// </summary>
public sealed class Retrabalho : Entity
{
    public const int GidMaxLength = 50;

    public const int ReabertaPorMaxLength = 200;

    internal Retrabalho(short numero, DateTime reabertaEm, string? reabertaPor, string? asanaStoryGid, DateOnly? prazoInternoAnterior, DateTime? concluidaEm = null)
    {
        Numero = numero;
        ConcluidaEm = concluidaEm;
        ReabertaEm = reabertaEm;
        ReabertaPor = reabertaPor;
        AsanaStoryGid = asanaStoryGid;
        PrazoInternoAnterior = prazoInternoAnterior;
    }

    /// <summary>
    /// Reopened task
    /// </summary>
    public long TarefaId { get; private set; }

    /// <summary>
    /// Sequence of the rework in the task, starting at 1
    /// </summary>
    public short Numero { get; private set; }

    /// <summary>
    /// Local date and time of the completion that preceded the reopening (from the Asana history); <see langword="null"/>
    /// for reworks recorded before the history was reprocessed
    /// </summary>
    public DateTime? ConcluidaEm { get; private set; }

    /// <summary>
    /// Local date and time of the reopening (from the Asana history, or of the synchronization when not found)
    /// </summary>
    public DateTime ReabertaEm { get; private set; }

    /// <summary>
    /// Who reopened the task; <see langword="null"/> when not found in the history
    /// </summary>
    public string? ReabertaPor { get; private set; }

    /// <summary>
    /// Asana story of the reopening; <see langword="null"/> when not found in the history
    /// </summary>
    public string? AsanaStoryGid { get; private set; }

    /// <summary>
    /// Internal due date in force when the task was reopened
    /// </summary>
    public DateOnly? PrazoInternoAnterior { get; private set; }

    /// <summary>
    /// Internal due date set after the reopening; <see langword="null"/> while pending
    /// </summary>
    public DateOnly? PrazoInternoNovo { get; set; }

    /// <summary>
    /// When the rework was completed; <see langword="null"/> while open
    /// </summary>
    public DateTime? ResolvidoEm { get; set; }

    /// <summary>
    /// Update the rework from its pair in the Asana history, keeping what the Actum recorded (due dates and the
    /// resolution set by a completion in the Actum)
    /// </summary>
    internal void AtualizarDoHistorico(short numero, ParDoHistorico par)
    {
        Numero = numero;
        ConcluidaEm = par.ConcluidaEm;
        ReabertaEm = par.ReabertaEm;
        ReabertaPor = par.ReabertaPor ?? ReabertaPor;
        ResolvidoEm ??= par.ResolvidoEm;
    }
}
