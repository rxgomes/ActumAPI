using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Application;

/// <summary>
/// Outcome of a lifecycle action on a task
/// </summary>
public enum TarefaResultadoStatus
{
    /// <summary>
    /// The action was applied
    /// </summary>
    Sucesso,

    /// <summary>
    /// No task (or branch) has the given identifier
    /// </summary>
    NaoEncontrada,

    /// <summary>
    /// The task already has a branch with the same name
    /// </summary>
    BranchRepetida,
}

/// <summary>
/// Result of a lifecycle action: the task state after the action, or the reason it was not applied. Business
/// rule violations are not results: they throw <see cref="Actum.Shared.Abstractions.Domain.DomainException"/>.
/// </summary>
/// <param name="Status">Outcome</param>
/// <param name="Tarefa">Task after the action, present only when <paramref name="Status"/> is <see cref="TarefaResultadoStatus.Sucesso"/></param>
/// <param name="Branch">Branch registered, present only for a successful branch registration</param>
public sealed record TarefaResultado(TarefaResultadoStatus Status, TarefaCicloResponse? Tarefa = null, BranchResponse? Branch = null)
{
    /// <summary>
    /// No task (or branch) has the given identifier
    /// </summary>
    public static readonly TarefaResultado NaoEncontrada = new(TarefaResultadoStatus.NaoEncontrada);

    /// <summary>
    /// The task already has a branch with the same name
    /// </summary>
    public static readonly TarefaResultado BranchRepetida = new(TarefaResultadoStatus.BranchRepetida);

    /// <summary>
    /// The action was applied
    /// </summary>
    /// <param name="tarefa">Task after the action</param>
    /// <param name="branch">Branch registered, for a branch registration</param>
    /// <returns>Successful result</returns>
    public static TarefaResultado Sucesso(TarefaCicloResponse tarefa, BranchResponse? branch = null)
    {
        return new TarefaResultado(TarefaResultadoStatus.Sucesso, tarefa, branch);
    }
}
