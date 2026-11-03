using System.ComponentModel.DataAnnotations;

namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Change of a company; fields not sent are kept
/// </summary>
/// <param name="Cor">New color in hex (<c>#RRGGBB</c>)</param>
/// <param name="Sincronizar">Whether the synchronization reads the tasks of this company</param>
public sealed record AlterarEmpresaRequest(
    [property: RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "A cor deve estar no formato #RRGGBB.")] string? Cor,
    bool? Sincronizar);
