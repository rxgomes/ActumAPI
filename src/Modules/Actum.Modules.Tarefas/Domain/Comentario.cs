using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Comment of a task, copied from the Asana history
/// </summary>
public sealed class Comentario : Entity
{
    public const int GidMaxLength = 50;

    public const int AutorMaxLength = 200;

    /// <summary>
    /// Create a comment
    /// </summary>
    /// <param name="asanaStoryGid">Story identifier in the Asana (unique)</param>
    /// <param name="autor">Author name</param>
    /// <param name="texto">Comment text</param>
    /// <param name="criadoEm">Local date and time of the comment</param>
    /// <param name="lido">Whether the user already saw it</param>
    public Comentario(string asanaStoryGid, string? autor, string texto, DateTime criadoEm, bool lido)
    {
        AsanaStoryGid = asanaStoryGid;
        Autor = autor;
        Texto = texto;
        CriadoEm = criadoEm;
        Lido = lido;
    }

    /// <summary>
    /// Task the comment belongs to
    /// </summary>
    public long TarefaId { get; private set; }

    /// <summary>
    /// Story identifier in the Asana (unique)
    /// </summary>
    public string AsanaStoryGid { get; private set; }

    /// <summary>
    /// Author name; <see langword="null"/> when unknown
    /// </summary>
    public string? Autor { get; private set; }

    /// <summary>
    /// Comment text
    /// </summary>
    public string Texto { get; private set; }

    /// <summary>
    /// Local date and time of the comment in the Asana
    /// </summary>
    public DateTime CriadoEm { get; private set; }

    /// <summary>
    /// Whether the user already saw the comment
    /// </summary>
    public bool Lido { get; set; }
}
