namespace Actum.Modules.Asana.Contracts;

/// <summary>
/// Asana tag
/// </summary>
/// <param name="Gid">Tag identifier in the Asana</param>
/// <param name="Nome">Tag name</param>
public sealed record AsanaTag(string Gid, string Nome);
