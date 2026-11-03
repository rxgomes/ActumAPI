namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Internal status of a task in the Actum (stored as snake_case text, e.g. <c>em_andamento</c>)
/// </summary>
public enum StatusTarefa
{
    /// <summary>
    /// New task waiting to be started (inbox)
    /// </summary>
    Entrada,

    /// <summary>
    /// Task in progress
    /// </summary>
    EmAndamento,

    /// <summary>
    /// Task blocked; the status to return to is kept in <see cref="Tarefa.StatusAnterior"/>
    /// </summary>
    Bloqueada,

    /// <summary>
    /// Task completed by the Actum, waiting for test in the Asana
    /// </summary>
    Concluida,

    /// <summary>
    /// Task reopened in the Asana after being completed
    /// </summary>
    Retrabalho,

    /// <summary>
    /// Task that left the open list of the user in the Asana; the reason is in <see cref="Tarefa.MotivoArquivamento"/>
    /// </summary>
    Arquivada,
}
