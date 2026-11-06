using System.Text.Json.Serialization;
using Actum.Modules.Tarefas.Contracts;

namespace Actum.Modules.Tarefas.Domain;

/// <summary>
/// Part of the system a branch belongs to (JSON as snake_case: <c>front</c> or <c>back</c>); each part has its own
/// GitHub repository in the company
/// </summary>
/// <remarks>
/// The converter is on the type (not on the properties) because the parts travel as lists, whose items do not see
/// a property-level converter.
/// </remarks>
[JsonConverter(typeof(SnakeCaseEnumJsonConverter<ParteBranch>))]
public enum ParteBranch
{
    /// <summary>
    /// Front-end (repository <see cref="Empresa.RepositorioFront"/>)
    /// </summary>
    Front,

    /// <summary>
    /// Back-end (repository <see cref="Empresa.RepositorioBack"/>)
    /// </summary>
    Back,
}
