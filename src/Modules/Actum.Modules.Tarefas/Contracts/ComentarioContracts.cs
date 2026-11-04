namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Person following the task in the Asana, who can be mentioned in a comment
/// </summary>
/// <param name="Gid">User identifier in the Asana (sent back in <c>mencoes</c>)</param>
/// <param name="Nome">User name</param>
/// <param name="Sugerido">Whether this is the suggested person to mention (who reopened or last commented)</param>
public sealed record ParticipanteResponse(string Gid, string Nome, bool Sugerido);

/// <summary>
/// Comment published in the Asana and recorded in the Actum
/// </summary>
/// <param name="Comentario">Comment as it appears in the task detail</param>
/// <param name="Anexos">Names of the files attached to the task, in order</param>
public sealed record ComentarioEnviadoResponse(ComentarioResponse Comentario, IReadOnlyList<string> Anexos);
