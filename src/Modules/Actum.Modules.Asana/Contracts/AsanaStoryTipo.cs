namespace Actum.Modules.Asana.Contracts;

/// <summary>
/// Kind of an entry (story) in the task history
/// </summary>
public enum AsanaStoryTipo
{
    /// <summary>
    /// Any other kind of story
    /// </summary>
    Outro,

    /// <summary>
    /// Comment added to the task (<c>comment_added</c>)
    /// </summary>
    Comentario,

    /// <summary>
    /// Task marked as incomplete, i.e. reopened (<c>marked_incomplete</c>)
    /// </summary>
    MarcadaIncompleta,

    /// <summary>
    /// Task marked as completed (<c>marked_complete</c>)
    /// </summary>
    MarcadaConcluida,
}
