namespace Actum.Modules.Asana.Contracts;

/// <summary>
/// User authenticated by the configured Asana token
/// </summary>
/// <param name="Gid">User identifier in the Asana</param>
/// <param name="Nome">User name</param>
/// <param name="Workspaces">Workspaces the user can access</param>
public sealed record AsanaUsuario(string Gid, string Nome, IReadOnlyList<AsanaWorkspace> Workspaces);
