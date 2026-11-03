namespace Actum.Modules.Asana.Contracts;

/// <summary>
/// Asana task
/// </summary>
/// <param name="Gid">Task identifier in the Asana</param>
/// <param name="Nome">Task name</param>
/// <param name="Notas">Task description in plain text</param>
/// <param name="NotasHtml">Task description in HTML</param>
/// <param name="PrazoEm">Due date, or <see langword="null"/> when the task has no due date</param>
/// <param name="Concluida">Indicates whether the task is completed</param>
/// <param name="ModificadaEm">Local date and time of the last change in the Asana</param>
/// <param name="Link">Task URL in the Asana</param>
/// <param name="Projetos">Names of the projects the task belongs to</param>
/// <param name="Tags">Task tags</param>
/// <param name="ResponsavelGid">Identifier of the assignee, or <see langword="null"/> when unassigned</param>
public sealed record AsanaTarefa(
    string Gid,
    string Nome,
    string Notas,
    string NotasHtml,
    DateOnly? PrazoEm,
    bool Concluida,
    DateTime ModificadaEm,
    string Link,
    IReadOnlyList<string> Projetos,
    IReadOnlyList<AsanaTag> Tags,
    string? ResponsavelGid);
