namespace Actum.Modules.Asana.Contracts;

/// <summary>
/// Entry of the task history (comment or system event)
/// </summary>
/// <param name="Gid">Story identifier in the Asana</param>
/// <param name="Tipo">Story kind</param>
/// <param name="CriadaEm">Local date and time when the story was created</param>
/// <param name="AutorNome">Author name, or <see langword="null"/> when created by the Asana itself</param>
/// <param name="Texto">Story text (the comment, for comments)</param>
public sealed record AsanaStory(string Gid, AsanaStoryTipo Tipo, DateTime CriadaEm, string? AutorNome, string Texto);
