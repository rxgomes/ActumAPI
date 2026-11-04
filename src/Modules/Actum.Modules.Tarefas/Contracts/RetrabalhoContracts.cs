using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Actum.Modules.Tarefas.Domain;

namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// Period and company of the rework indicators
/// </summary>
public sealed class IndicadoresFiltro : IValidatableObject
{
    /// <summary>
    /// Longest period of the indicators, in months
    /// </summary>
    public const int MaximoDeMeses = 24;

    /// <summary>
    /// First day of the period (reopening date)
    /// </summary>
    [Required(ErrorMessage = "Informe o início do período.")]
    public DateOnly? De { get; init; }

    /// <summary>
    /// Last day of the period (reopening date), at most 24 months after <see cref="De"/>
    /// </summary>
    [Required(ErrorMessage = "Informe o fim do período.")]
    public DateOnly? Ate { get; init; }

    /// <summary>
    /// Company identifier; all companies when not informed
    /// </summary>
    public Guid? Empresa { get; init; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (De is not { } de || Ate is not { } ate)
        {
            yield break;
        }

        if (de > ate)
        {
            yield return new ValidationResult("O início do período deve ser anterior ou igual ao fim.", [nameof(Ate)]);
        }
        else if (ate > de.AddMonths(MaximoDeMeses))
        {
            yield return new ValidationResult($"O período deve ter no máximo {MaximoDeMeses} meses.", [nameof(Ate)]);
        }
    }
}

/// <summary>
/// Rework indicators of the period, per company and in total
/// </summary>
/// <param name="De">First day of the period</param>
/// <param name="Ate">Last day of the period</param>
/// <param name="Empresas">Indicators of each company of the filter</param>
/// <param name="Total">Indicators of all companies of the filter together</param>
/// <param name="Evolucao">Reworks per month of the period and company, including months without rework</param>
/// <param name="Ranking">Up to 10 tasks with more reworks in the period</param>
/// <param name="UltimoReprocessamentoEm">When the Asana history was last reprocessed; <see langword="null"/> when never</param>
public sealed record IndicadoresResponse(
    DateOnly De,
    DateOnly Ate,
    IReadOnlyList<IndicadorDaEmpresaResponse> Empresas,
    IndicadorDaEmpresaResponse Total,
    IReadOnlyList<MesDeRetrabalhoResponse> Evolucao,
    IReadOnlyList<TarefaDoRankingResponse> Ranking,
    DateTime? UltimoReprocessamentoEm);

/// <summary>
/// Rework indicators of a company (or of the total)
/// </summary>
/// <param name="Identifier">Company identifier; <see langword="null"/> in the total</param>
/// <param name="Nome">Company name, or "Total"</param>
/// <param name="Cor">Company color; <see langword="null"/> in the total</param>
/// <param name="Retrabalhos">Reworks reopened in the period</param>
/// <param name="TarefasComRetrabalho">Tasks with at least one rework reopened in the period</param>
/// <param name="TarefasConcluidas">Tasks completed at least once in the period</param>
/// <param name="Taxa">Percentage (one decimal) of the tasks completed in the period that were reopened after a completion of the period; <see langword="null"/> without completions</param>
public sealed record IndicadorDaEmpresaResponse(
    Guid? Identifier,
    string Nome,
    string? Cor,
    int Retrabalhos,
    int TarefasComRetrabalho,
    int TarefasConcluidas,
    decimal? Taxa);

/// <summary>
/// Reworks of a month, per company
/// </summary>
/// <param name="Mes">Month, as <c>yyyy-MM</c></param>
/// <param name="PorEmpresa">Reworks of each company of the filter in the month</param>
public sealed record MesDeRetrabalhoResponse(string Mes, IReadOnlyList<RetrabalhosDaEmpresaNoMesResponse> PorEmpresa);

/// <summary>
/// Reworks of a company in a month
/// </summary>
/// <param name="Identifier">Company identifier</param>
/// <param name="Quantidade">Reworks reopened in the month</param>
public sealed record RetrabalhosDaEmpresaNoMesResponse(Guid Identifier, int Quantidade);

/// <summary>
/// Task in the ranking of reworks of the period
/// </summary>
/// <param name="Identifier">Task identifier (opens the detail)</param>
/// <param name="Titulo">Task title</param>
/// <param name="Empresa">Company of the task</param>
/// <param name="Status">Internal status</param>
/// <param name="Retrabalhos">Reworks reopened in the period</param>
/// <param name="UltimaReaberturaEm">Last reopening in the period</param>
public sealed record TarefaDoRankingResponse(
    Guid Identifier,
    string Titulo,
    EmpresaResumoResponse Empresa,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<StatusTarefa>))] StatusTarefa Status,
    int Retrabalhos,
    DateTime UltimaReaberturaEm);

/// <summary>
/// Summary of the reprocessing of the Asana history
/// </summary>
/// <param name="TarefasLidas">Tasks whose history was read</param>
/// <param name="RetrabalhosAntes">Reworks recorded before the reprocessing</param>
/// <param name="RetrabalhosDepois">Reworks recorded after the reprocessing</param>
/// <param name="Falhas">Tasks whose history could not be read (they keep their reworks)</param>
public sealed record ReprocessamentoResponse(
    int TarefasLidas,
    int RetrabalhosAntes,
    int RetrabalhosDepois,
    IReadOnlyList<FalhaDoReprocessamentoResponse> Falhas);

/// <summary>
/// Task whose Asana history could not be read in the reprocessing
/// </summary>
/// <param name="Identifier">Task identifier</param>
/// <param name="Titulo">Task title</param>
/// <param name="Erro">Failure, in pt-BR</param>
public sealed record FalhaDoReprocessamentoResponse(Guid Identifier, string Titulo, string Erro);
