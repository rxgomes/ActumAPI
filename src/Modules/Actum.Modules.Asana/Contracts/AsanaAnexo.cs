namespace Actum.Modules.Asana.Contracts;

/// <summary>
/// File attached to an Asana task
/// </summary>
/// <param name="Gid">Attachment identifier in the Asana</param>
/// <param name="Nome">File name</param>
public sealed record AsanaAnexo(string Gid, string Nome);
