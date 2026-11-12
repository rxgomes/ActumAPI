using System.Text.Json.Serialization;
using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Situation of a worktree or of one of its parts (JSON and database as snake_case: <c>criado</c>, <c>falhou</c>,
/// <c>removido</c>)
/// </summary>
[JsonConverter(typeof(SnakeCaseEnumJsonConverter<SituacaoWorktree>))]
public enum SituacaoWorktree
{
    /// <summary>
    /// The worktree exists in the clone, on the branch of the task
    /// </summary>
    Criado,

    /// <summary>
    /// The last attempt failed (see the error); it can be retried
    /// </summary>
    Falhou,

    /// <summary>
    /// The worktree was removed from the clone
    /// </summary>
    Removido,
}
