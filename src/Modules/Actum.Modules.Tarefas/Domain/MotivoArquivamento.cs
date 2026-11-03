namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Why a task was archived (stored as snake_case text, e.g. <c>concluida_no_asana</c>)
/// </summary>
public enum MotivoArquivamento
{
    /// <summary>
    /// Completed directly in the Asana, not by the Actum
    /// </summary>
    ConcluidaNoAsana,

    /// <summary>
    /// Assigned to another person in the Asana
    /// </summary>
    Reatribuida,

    /// <summary>
    /// Deleted in the Asana (or no longer accessible)
    /// </summary>
    Excluida,
}
