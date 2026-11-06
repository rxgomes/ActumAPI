using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Actum.Modules.Tarefas.Domain;

namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Start of a task from the inbox
/// </summary>
/// <param name="BranchBase">Base branch name (e.g. <c>main</c>)</param>
/// <param name="BranchImplementacao">Implementation branch name (e.g. <c>feature/ipi-devolvido</c>)</param>
/// <param name="PrazoInterno">Internal due date</param>
/// <param name="Partes">Parts the branches belong to (<c>front</c> and/or <c>back</c>): the implementation branch is created in the GitHub repository of each part, from the last commit of the base branch</param>
public sealed record IniciarTarefaRequest(
    [property: Required(ErrorMessage = "A branch base é obrigatória.")]
    [property: StringLength(TarefaBranch.NomeMaxLength, ErrorMessage = "A branch base deve ter no máximo {1} caracteres.")]
    string? BranchBase,
    [property: Required(ErrorMessage = "A branch de implementação é obrigatória.")]
    [property: StringLength(TarefaBranch.NomeMaxLength, ErrorMessage = "A branch de implementação deve ter no máximo {1} caracteres.")]
    string? BranchImplementacao,
    [property: Required(ErrorMessage = "O prazo interno é obrigatório.")] DateOnly? PrazoInterno,
    [property: Required(ErrorMessage = TarefaRequestsMensagens.PartesObrigatorias)]
    [property: MinLength(1, ErrorMessage = TarefaRequestsMensagens.PartesObrigatorias)]
    IReadOnlyList<ParteBranch>? Partes);

/// <summary>
/// New branch created when resuming a paused task, usually after a reopening (rework)
/// </summary>
/// <param name="BranchBase">Base branch name (e.g. <c>main</c>); when the task already has it as a base branch, it is only checked in the GitHub</param>
/// <param name="BranchImplementacao">New implementation branch name, not yet registered in the task</param>
/// <param name="Partes">Parts the new branches belong to (<c>front</c> and/or <c>back</c>): the implementation branch is created in the GitHub repository of each part, from the last commit of the base branch</param>
public sealed record RetomarTarefaRequest(
    [property: Required(ErrorMessage = "A branch base é obrigatória.")]
    [property: StringLength(TarefaBranch.NomeMaxLength, ErrorMessage = "A branch base deve ter no máximo {1} caracteres.")]
    string? BranchBase,
    [property: Required(ErrorMessage = "A branch de implementação é obrigatória.")]
    [property: StringLength(TarefaBranch.NomeMaxLength, ErrorMessage = "A branch de implementação deve ter no máximo {1} caracteres.")]
    string? BranchImplementacao,
    [property: Required(ErrorMessage = TarefaRequestsMensagens.PartesObrigatorias)]
    [property: MinLength(1, ErrorMessage = TarefaRequestsMensagens.PartesObrigatorias)]
    IReadOnlyList<ParteBranch>? Partes);

/// <summary>
/// Change of the internal due date and/or priority; fields not sent are kept
/// </summary>
/// <param name="PrazoInterno">New internal due date</param>
/// <param name="Prioridade">New priority, from 1 (highest) to 3</param>
public sealed record AlterarTarefaRequest(
    DateOnly? PrazoInterno,
    [property: Range(1, 3, ErrorMessage = "A prioridade deve estar entre {1} e {2}.")] short? Prioridade);

/// <summary>
/// Registration of a branch in a task
/// </summary>
/// <param name="Tipo">Role: <c>base</c> or <c>implementacao</c></param>
/// <param name="Nome">Branch name</param>
/// <param name="Partes">Parts the branch belongs to (<c>front</c> and/or <c>back</c>): a <c>base</c> branch must exist in the GitHub repository of each part; an <c>implementacao</c> branch is created in each of them from the most recent base branch of the task</param>
public sealed record AdicionarBranchRequest(
    [property: Required(ErrorMessage = "O tipo da branch é obrigatório.")]
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<TipoBranch>))]
    TipoBranch? Tipo,
    [property: Required(ErrorMessage = "O nome da branch é obrigatório.")]
    [property: StringLength(TarefaBranch.NomeMaxLength, ErrorMessage = "O nome da branch deve ter no máximo {1} caracteres.")]
    string? Nome,
    [property: Required(ErrorMessage = TarefaRequestsMensagens.PartesObrigatorias)]
    [property: MinLength(1, ErrorMessage = TarefaRequestsMensagens.PartesObrigatorias)]
    IReadOnlyList<ParteBranch>? Partes);

/// <summary>
/// Validation messages shared by the task requests
/// </summary>
internal static class TarefaRequestsMensagens
{
    public const string PartesObrigatorias = "Informe se a branch é do front, do back ou dos dois.";
}
