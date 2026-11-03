using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Filters of the board (<c>GET /tarefas/painel</c>)
/// </summary>
public sealed record PainelFiltro
{
    /// <summary>
    /// Company identifier; without it, every company is considered
    /// </summary>
    [FromQuery(Name = "empresa")]
    public Guid? Empresa { get; init; }

    /// <summary>
    /// Text searched in the title and the description, ignoring case and accents
    /// </summary>
    [FromQuery(Name = "busca")]
    [StringLength(200, ErrorMessage = "A busca deve ter no máximo {1} caracteres.")]
    public string? Busca { get; init; }
}

/// <summary>
/// Filters and page of the task list (<c>GET /tarefas</c>)
/// </summary>
public sealed record ListarTarefasFiltro
{
    /// <summary>
    /// Default page size
    /// </summary>
    public const int TamanhoPadrao = 20;

    /// <summary>
    /// Largest page size accepted
    /// </summary>
    public const int TamanhoMaximo = 100;

    /// <summary>
    /// Company identifier; without it, every company is considered
    /// </summary>
    [FromQuery(Name = "empresa")]
    public Guid? Empresa { get; init; }

    /// <summary>
    /// Status: <c>entrada</c>, <c>em_andamento</c>, <c>bloqueada</c>, <c>concluida</c>, <c>retrabalho</c> or
    /// <c>arquivada</c>; without it, every status is considered
    /// </summary>
    [FromQuery(Name = "status")]
    [AllowedValues(null, "entrada", "em_andamento", "bloqueada", "concluida", "retrabalho", "arquivada",
        ErrorMessage = "O status deve ser entrada, em_andamento, bloqueada, concluida, retrabalho ou arquivada.")]
    public string? Status { get; init; }

    /// <summary>
    /// Text searched in the title and the description, ignoring case and accents
    /// </summary>
    [FromQuery(Name = "busca")]
    [StringLength(200, ErrorMessage = "A busca deve ter no máximo {1} caracteres.")]
    public string? Busca { get; init; }

    /// <summary>
    /// Page number, starting at 1 (default 1)
    /// </summary>
    [FromQuery(Name = "pagina")]
    [Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior ou igual a {1}.")]
    public int? Pagina { get; init; }

    /// <summary>
    /// Page size, from 1 to 100 (default 20)
    /// </summary>
    [FromQuery(Name = "tamanho")]
    [Range(1, TamanhoMaximo, ErrorMessage = "O tamanho da página deve estar entre {1} e {2}.")]
    public int? Tamanho { get; init; }
}
