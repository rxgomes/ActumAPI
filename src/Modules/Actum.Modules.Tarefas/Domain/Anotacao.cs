using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Private note of a task, kept only in the Actum and never sent to the Asana. Created at <see cref="Entity.CreateIn"/>.
/// </summary>
public sealed class Anotacao : Entity
{
    public const int TextoMaxLength = 10_000;

    private Anotacao()
    {
    }

    /// <summary>
    /// Task of the note
    /// </summary>
    public long TarefaId { get; private set; }

    /// <summary>
    /// Note text
    /// </summary>
    public string Texto { get; private set; } = string.Empty;

    /// <summary>
    /// Local date and time of the last change of the text; <see langword="null"/> when never changed
    /// </summary>
    public DateTime? AlteradaEm { get; private set; }

    /// <summary>
    /// Create a note for the task
    /// </summary>
    /// <param name="tarefaId">Task of the note</param>
    /// <param name="texto">Note text, already trimmed and validated</param>
    /// <returns>New note</returns>
    public static Anotacao Criar(long tarefaId, string texto)
    {
        return new Anotacao { TarefaId = tarefaId, Texto = texto };
    }

    /// <summary>
    /// Change the text of the note
    /// </summary>
    /// <param name="texto">New text, already trimmed and validated</param>
    /// <param name="agora">Local date and time of the change</param>
    public void Alterar(string texto, DateTime agora)
    {
        Texto = texto;
        AlteradaEm = agora;
    }
}
