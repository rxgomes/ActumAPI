namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Kind of run recorded in <c>sincronizacao</c> (stored as snake_case text)
/// </summary>
public enum TipoSincronizacao
{
    /// <summary>
    /// Synchronization of the open tasks with the Asana
    /// </summary>
    Sincronizacao,

    /// <summary>
    /// Reprocessing of the Asana history of every task to recalculate completions and reworks
    /// </summary>
    Reprocessamento,
}
