using System.ComponentModel.DataAnnotations;
using Actum.Modules.Tarefas.Domain;

namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Change of a company; fields not sent (<see langword="null"/>) are kept
/// </summary>
/// <param name="Cor">New color in hex (<c>#RRGGBB</c>)</param>
/// <param name="Sincronizar">Whether the synchronization reads the tasks of this company</param>
/// <param name="RepositorioFront">GitHub repository of the front-end (<c>owner/repo</c>); empty text removes it</param>
/// <param name="RepositorioBack">GitHub repository of the back-end (<c>owner/repo</c>); empty text removes it</param>
public sealed record AlterarEmpresaRequest(
    [property: RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "A cor deve estar no formato #RRGGBB.")] string? Cor,
    bool? Sincronizar,
    [property: RegularExpression(AlterarEmpresaRequest.FormatoDoRepositorio, ErrorMessage = "O repositório de front deve estar no formato dono/repo (ex.: ixact/erp-web).")]
    [property: StringLength(Empresa.RepositorioMaxLength, ErrorMessage = "O repositório de front deve ter no máximo {1} caracteres.")]
    string? RepositorioFront = null,
    [property: RegularExpression(AlterarEmpresaRequest.FormatoDoRepositorio, ErrorMessage = "O repositório de back deve estar no formato dono/repo (ex.: ixact/erp-api).")]
    [property: StringLength(Empresa.RepositorioMaxLength, ErrorMessage = "O repositório de back deve ter no máximo {1} caracteres.")]
    string? RepositorioBack = null)
{
    /// <summary>
    /// <c>owner/repo</c> of the GitHub (spaces at the ends allowed, removed when saved), or empty text
    /// </summary>
    public const string FormatoDoRepositorio = @"^\s*$|^\s*[A-Za-z0-9-]+/[A-Za-z0-9._-]+\s*$";
}
