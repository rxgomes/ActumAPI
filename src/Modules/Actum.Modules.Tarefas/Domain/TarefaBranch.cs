using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Git branch of a task. On completion it becomes an Asana tag with the text of <see cref="Tag"/>.
/// </summary>
public sealed class TarefaBranch : Entity
{
    public const int NomeMaxLength = 250;

    public const int TipoMaxLength = 20;

    public const int GidMaxLength = 50;

    /// <summary>
    /// Used by the EF Core when reading; the parts are filled by their properties
    /// </summary>
    private TarefaBranch(TipoBranch tipo, string nome, long? retrabalhoId)
    {
        Tipo = tipo;
        Nome = nome;
        RetrabalhoId = retrabalhoId;
    }

    internal TarefaBranch(TipoBranch tipo, string nome, long? retrabalhoId, IEnumerable<ParteBranch> partes)
        : this(tipo, nome, retrabalhoId)
    {
        var conjunto = partes.ToHashSet();
        ParteFront = conjunto.Contains(ParteBranch.Front);
        ParteBack = conjunto.Contains(ParteBranch.Back);
    }

    /// <summary>
    /// Whether the branch belongs to the front-end
    /// </summary>
    public bool ParteFront { get; private set; }

    /// <summary>
    /// Whether the branch belongs to the back-end
    /// </summary>
    public bool ParteBack { get; private set; }

    /// <summary>
    /// Parts of the branch, in the order front, back; empty for branches registered before the parts existed
    /// </summary>
    public IReadOnlyList<ParteBranch> Partes
    {
        get
        {
            var partes = new List<ParteBranch>(2);
            if (ParteFront)
            {
                partes.Add(ParteBranch.Front);
            }

            if (ParteBack)
            {
                partes.Add(ParteBranch.Back);
            }

            return partes;
        }
    }

    /// <summary>
    /// Text of the Asana tag: <c>(Front, Back) => nome</c>, <c>(Front) => nome</c> or <c>(Back) => nome</c>;
    /// just the name when the branch has no parts
    /// </summary>
    public string Tag
    {
        get
        {
            var partes = Partes;
            return partes.Count == 0
                ? Nome
                : $"({string.Join(", ", partes.Select(parte => parte.ToString()))}) => {Nome}";
        }
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
    /// Branch name, without spaces at the ends
    /// </summary>
    public string Nome { get; private set; }

    /// <summary>
    /// Rework open when the branch was registered; <see langword="null"/> otherwise
    /// </summary>
    public long? RetrabalhoId { get; private set; }

    /// <summary>
    /// Remove the link with a rework that no longer exists (the branch itself is kept)
    /// </summary>
    internal void DesvincularRetrabalho()
    {
        RetrabalhoId = null;
    }

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
