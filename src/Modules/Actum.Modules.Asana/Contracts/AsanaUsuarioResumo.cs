namespace Actum.Modules.Asana.Contracts;

/// <summary>
/// Asana user that can be mentioned in a comment
/// </summary>
/// <param name="Gid">User identifier in the Asana</param>
/// <param name="Nome">User name</param>
public sealed record AsanaUsuarioResumo(string Gid, string Nome);
