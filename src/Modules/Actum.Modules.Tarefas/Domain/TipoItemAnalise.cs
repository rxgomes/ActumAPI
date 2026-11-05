namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Kind of an analysis item (stored as snake_case text: <c>checklist</c>, <c>teste</c>, <c>duvida</c> or <c>regra</c>)
/// </summary>
public enum TipoItemAnalise
{
    /// <summary>
    /// Action to do; marked means done
    /// </summary>
    Checklist,

    /// <summary>
    /// How to validate the delivery; marked means tested
    /// </summary>
    Teste,

    /// <summary>
    /// Question to the requester; marked means resolved
    /// </summary>
    Duvida,

    /// <summary>
    /// Rule, constraint or suggestion to respect, with its origin; marked means respected
    /// </summary>
    Regra,
}
