namespace Actum.Modules.Asana.Contracts;

/// <summary>
/// Asana workspace accessible by the authenticated user
/// </summary>
/// <param name="Gid">Workspace identifier in the Asana</param>
/// <param name="Nome">Workspace name</param>
public sealed record AsanaWorkspace(string Gid, string Nome);
