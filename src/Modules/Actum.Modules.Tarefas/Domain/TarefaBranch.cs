using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Git branch of a task. On completion it becomes an Asana tag with exactly the same name.
/// </summary>
public sealed class TarefaBranch : Entity
{
    public const int NomeMaxLength = 250;

    public const int TipoMaxLength = 20;

    public const int GidMaxLength = 50;

    internal TarefaBranch(TipoBranch tipo, string nome, long? retrabalhoId)
    {
        Tipo = tipo;
        Nome = nome;
        RetrabalhoId = retrabalhoId;
    }

    /// <summary>
    /// Task of the branch
    /// </summary>
    public long TarefaId { get; private set; }

    /// <summary>
    /// Role of the branch
    /// </summary>
    public TipoBranch Tipo { get; private set; }

    /// <summary>
    /// Branch name, without spaces at the ends; also the tag name in the Asana
    /// </summary>
    public string Nome { get; private set; }

    /// <summary>
    /// Rework open when the branch was registered; <see langword="null"/> otherwise
    /// </summary>
    public long? RetrabalhoId { get; private set; }

    /// <summary>
    /// Identifier of the Asana tag, filled when sent
    /// </summary>
    public string? AsanaTagGid { get; private set; }

    /// <summary>
    /// When the tag was added in the Asana; <see langword="null"/> until then
    /// </summary>
    public DateTime? EnviadaAsanaEm { get; private set; }

    /// <summary>
    /// Whether the tag is already in the Asana task
    /// </summary>
    public bool Enviada => EnviadaAsanaEm is not null;

    /// <summary>
    /// Record that the tag is in the Asana task
    /// </summary>
    /// <param name="asanaTagGid">Tag identifier in the Asana</param>
    /// <param name="agora">Local date and time</param>
    public void MarcarEnviada(string asanaTagGid, DateTime agora)
    {
        AsanaTagGid = asanaTagGid;
        EnviadaAsanaEm = agora;
    }
}
