namespace Actum.Modules.Asana.Contracts;

/// <summary>
/// State of the connection with the Asana
/// </summary>
/// <param name="Configurado">Indicates whether the Asana token is configured</param>
/// <param name="Valido">Indicates whether the Asana accepted the token</param>
/// <param name="Usuario">Name of the authenticated user, when the token is valid</param>
/// <param name="Workspaces">Workspaces accessible by the user, when the token is valid</param>
public sealed record AsanaConexaoResponse(
    bool Configurado,
    bool Valido,
    string? Usuario,
    IReadOnlyList<AsanaWorkspace> Workspaces);
