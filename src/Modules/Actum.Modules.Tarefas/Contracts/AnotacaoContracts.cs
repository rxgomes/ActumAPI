using System.ComponentModel.DataAnnotations;
using Actum.Modules.Tarefas.Domain;

namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Text of a private note
/// </summary>
/// <param name="Texto">Note text, up to 10,000 characters</param>
public sealed record AnotacaoRequest(
    [property: Required(ErrorMessage = AnotacaoRequest.TextoObrigatorio)]
    [property: StringLength(Anotacao.TextoMaxLength, ErrorMessage = "A anotação deve ter no máximo {1} caracteres.")]
    string? Texto)
{
    /// <summary>
    /// Message of an empty note
    /// </summary>
    public const string TextoObrigatorio = "Escreva a anotação.";
}

/// <summary>
/// Private note of a task
/// </summary>
/// <param name="Identifier">Note identifier</param>
/// <param name="Texto">Note text</param>
/// <param name="CriadaEm">Local date and time of the creation</param>
/// <param name="AlteradaEm">Local date and time of the last change; <see langword="null"/> when never changed</param>
public sealed record AnotacaoResponse(Guid Identifier, string Texto, DateTime CriadaEm, DateTime? AlteradaEm);
