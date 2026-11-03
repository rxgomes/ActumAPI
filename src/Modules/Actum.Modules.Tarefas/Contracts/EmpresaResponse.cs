namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Company (Asana workspace)
/// </summary>
/// <param name="Identifier">Company identifier</param>
/// <param name="Nome">Workspace name</param>
/// <param name="Cor">Color in hex (<c>#RRGGBB</c>)</param>
/// <param name="Sincronizar">Whether the synchronization reads the tasks of this company</param>
/// <param name="CargaInicialEm">When the initial load finished; <see langword="null"/> until the first synchronization</param>
public sealed record EmpresaResponse(Guid Identifier, string Nome, string Cor, bool Sincronizar, DateTime? CargaInicialEm);
