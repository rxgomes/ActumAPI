using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Company: one Asana workspace accessible by the user
/// </summary>
public sealed class Empresa : Entity
{
    public const int GidMaxLength = 50;

    public const int NomeMaxLength = 200;

    public const int CorLength = 7;

    public const int RepositorioMaxLength = 200;

    /// <summary>
    /// Fixed palette; new companies get the next color, in creation order
    /// </summary>
    public static readonly IReadOnlyList<string> Paleta =
        ["#1976D2", "#E64A19", "#388E3C", "#7B1FA2", "#F9A825", "#00838F", "#C2185B", "#5D4037"];

    /// <summary>
    /// Create a company for a workspace, with synchronization turned on
    /// </summary>
    /// <param name="asanaWorkspaceGid">Workspace identifier in the Asana</param>
    /// <param name="nome">Workspace name</param>
    /// <param name="cor">Color in hex (<c>#RRGGBB</c>)</param>
    public Empresa(string asanaWorkspaceGid, string nome, string cor)
    {
        AsanaWorkspaceGid = asanaWorkspaceGid;
        Nome = nome;
        Cor = cor;
    }

    /// <summary>
    /// Workspace identifier in the Asana (unique)
    /// </summary>
    public string AsanaWorkspaceGid { get; private set; }

    /// <summary>
    /// Workspace name, kept equal to the Asana
    /// </summary>
    public string Nome { get; set; }

    /// <summary>
    /// Color used by the front to identify the company (<c>#RRGGBB</c>)
    /// </summary>
    public string Cor { get; set; }

    /// <summary>
    /// Whether the synchronization reads the tasks of this company
    /// </summary>
    public bool Sincronizar { get; set; } = true;

    /// <summary>
    /// GitHub repository of the front-end (<c>owner/repo</c>); <see langword="null"/> when not defined
    /// </summary>
    public string? RepositorioFront { get; set; }

    /// <summary>
    /// GitHub repository of the back-end (<c>owner/repo</c>); <see langword="null"/> when not defined
    /// </summary>
    public string? RepositorioBack { get; set; }

    /// <summary>
    /// GitHub repository of a part, or <see langword="null"/> when not defined
    /// </summary>
    /// <param name="parte">Part of the system</param>
    /// <returns>Repository (<c>owner/repo</c>) or <see langword="null"/></returns>
    public string? Repositorio(ParteBranch parte)
    {
        return parte == ParteBranch.Front ? RepositorioFront : RepositorioBack;
    }

    /// <summary>
    /// When the first synchronization of the company finished; <see langword="null"/> until then
    /// </summary>
    public DateTime? CargaInicialEm { get; private set; }

    /// <summary>
    /// Whether the next synchronization is the initial load of the company
    /// </summary>
    public bool CargaInicialPendente => CargaInicialEm is null;

    /// <summary>
    /// Record that the initial load finished
    /// </summary>
    /// <param name="agora">Local date and time</param>
    public void ConcluirCargaInicial(DateTime agora)
    {
        CargaInicialEm ??= agora;
    }
}
