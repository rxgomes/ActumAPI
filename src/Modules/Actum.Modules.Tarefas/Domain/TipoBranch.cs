namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Role of a branch in the task (stored as snake_case text: <c>base</c> or <c>implementacao</c>)
/// </summary>
public enum TipoBranch
{
    /// <summary>
    /// Branch the implementation starts from and is merged into (e.g. <c>main</c>)
    /// </summary>
    Base,

    /// <summary>
    /// Branch where the change was implemented (e.g. <c>feature/ipi-devolvido</c>)
    /// </summary>
    Implementacao,
}
