using Actum.Shared.Abstractions.Domain;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Checklist item, test, question or rule of an analysis, markable as done, tested, resolved or respected
/// </summary>
public sealed class AnaliseItem : Entity
{
    public const int TipoMaxLength = 20;

    public const int GrupoMaxLength = 100;

    public const int OrigemMaxLength = 200;

    internal AnaliseItem(
        TipoItemAnalise tipo,
        short ordem,
        string texto,
        string? grupo,
        string? vago,
        DateTime? concluidoEm,
        string? origem = null)
    {
        Tipo = tipo;
        Ordem = ordem;
        Texto = texto;
        Grupo = grupo is { Length: > GrupoMaxLength } ? grupo[..GrupoMaxLength] : grupo;
        Vago = vago;
        ConcluidoEm = concluidoEm;
        Origem = origem is { Length: > OrigemMaxLength } ? origem[..OrigemMaxLength] : origem;
    }

    /// <summary>
    /// Analysis the item belongs to
    /// </summary>
    public long AnaliseId { get; private set; }

    /// <summary>
    /// Checklist item or question
    /// </summary>
    public TipoItemAnalise Tipo { get; private set; }

    /// <summary>
    /// Position among the items of the same type, starting at 1
    /// </summary>
    public short Ordem { get; private set; }

    /// <summary>
    /// Item text
    /// </summary>
    public string Texto { get; private set; }

    /// <summary>
    /// Area of a checklist item (e.g. <c>Front</c>, <c>Back: cálculo</c>); <see langword="null"/> for tests and questions
    /// </summary>
    public string? Grupo { get; private set; }

    /// <summary>
    /// Why a checklist item is vague in the demand; <see langword="null"/> when it is clear
    /// </summary>
    public string? Vago { get; private set; }

    /// <summary>
    /// Where a rule came from (<c>descrição</c> or <c>comentário de Maria em 04/11/2026 14:30</c>);
    /// <see langword="null"/> for the other types
    /// </summary>
    public string? Origem { get; private set; }

    /// <summary>
    /// Local date and time when the item was marked; <see langword="null"/> while not marked
    /// </summary>
    public DateTime? ConcluidoEm { get; private set; }

    /// <summary>
    /// Mark (done/resolved) or unmark the item. Marking an already marked item keeps the original date.
    /// </summary>
    /// <param name="concluido">Whether the item is done/resolved</param>
    /// <param name="agora">Local date and time of the marking</param>
    public void Marcar(bool concluido, DateTime agora)
    {
        if (!concluido)
        {
            ConcluidoEm = null;
        }
        else if (ConcluidoEm is null)
        {
            ConcluidoEm = agora;
        }
    }
}
