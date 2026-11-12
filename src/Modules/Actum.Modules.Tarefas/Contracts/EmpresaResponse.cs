namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Company (Asana workspace)
/// </summary>
/// <param name="Identifier">Company identifier</param>
/// <param name="Nome">Workspace name</param>
/// <param name="Cor">Color in hex (<c>#RRGGBB</c>)</param>
/// <param name="Sincronizar">Whether the synchronization reads the tasks of this company</param>
/// <param name="CargaInicialEm">When the initial load finished; <see langword="null"/> until the first synchronization</param>
/// <param name="RepositorioFront">GitHub repository of the front-end (<c>owner/repo</c>); <see langword="null"/> when not defined</param>
/// <param name="RepositorioBack">GitHub repository of the back-end (<c>owner/repo</c>); <see langword="null"/> when not defined</param>
/// <param name="CloneFront">Folder of the local clone of the front-end repository; <see langword="null"/> when not defined</param>
/// <param name="CloneBack">Folder of the local clone of the back-end repository; <see langword="null"/> when not defined</param>
/// <param name="PastaWorktrees">Root folder of the worktrees of the tasks (<c>&lt;pasta&gt;\&lt;branch&gt;\front</c> and <c>...\back</c>); <see langword="null"/> when not defined</param>
/// <param name="ArquivosFront">Files outside the git copied from the front-end clone to each new worktree (paths relative to the repository)</param>
/// <param name="ArquivosBack">Files outside the git copied from the back-end clone to each new worktree (paths relative to the repository)</param>
public sealed record EmpresaResponse(
    Guid Identifier,
    string Nome,
    string Cor,
    bool Sincronizar,
    DateTime? CargaInicialEm,
    string? RepositorioFront,
    string? RepositorioBack,
    string? CloneFront,
    string? CloneBack,
    string? PastaWorktrees,
    IReadOnlyList<string> ArquivosFront,
    IReadOnlyList<string> ArquivosBack);
