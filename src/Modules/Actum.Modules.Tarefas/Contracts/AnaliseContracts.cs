using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Actum.Modules.Tarefas.Domain;

namespace Actum.Modules.Tarefas.Contracts;

/// <summary>
/// One version of the AI analysis of a task
/// </summary>
/// <param name="Identifier">Analysis identifier</param>
/// <param name="Versao">Sequence in the task, starting at 1</param>
/// <param name="Atual">Whether this is the current (most recent) version; only its items can be marked</param>
/// <param name="Objetivo">Expected result of the task</param>
/// <param name="Checklist">Checklist grouped by area, groups and items in execution order</param>
/// <param name="Testes">How to validate the delivery, in order</param>
/// <param name="Duvidas">Questions to the requester, in order</param>
/// <param name="Regras">
/// Rules, constraints and suggestions to respect, in order, each with its origin; empty for versions analyzed before
/// the rules existed
/// </param>
/// <param name="Modelo">Model that answered</param>
/// <param name="TokensEntrada">Input tokens consumed</param>
/// <param name="TokensSaida">Output tokens consumed</param>
/// <param name="AnalisadaEm">Local date and time of the analysis</param>
public sealed record AnaliseResponse(
    Guid Identifier,
    short Versao,
    bool Atual,
    string Objetivo,
    IReadOnlyList<GrupoDoChecklistResponse> Checklist,
    IReadOnlyList<AnaliseItemResponse> Testes,
    IReadOnlyList<AnaliseItemResponse> Duvidas,
    IReadOnlyList<AnaliseItemResponse> Regras,
    string Modelo,
    int TokensEntrada,
    int TokensSaida,
    DateTime AnalisadaEm);

/// <summary>
/// Area of the checklist (e.g. <c>Front</c>, <c>Back: cálculo</c>) with its items
/// </summary>
/// <param name="Nome">Area name</param>
/// <param name="Itens">Items of the area, in order</param>
public sealed record GrupoDoChecklistResponse(string Nome, IReadOnlyList<AnaliseItemResponse> Itens);

/// <summary>
/// Checklist item, test, question or rule of an analysis
/// </summary>
/// <param name="Identifier">Item identifier</param>
/// <param name="Tipo"><c>checklist</c>, <c>teste</c>, <c>duvida</c> or <c>regra</c></param>
/// <param name="Ordem">Position among the items of the same type, starting at 1</param>
/// <param name="Texto">Item text</param>
/// <param name="Grupo">Area of a checklist item; <see langword="null"/> for the other types</param>
/// <param name="Vago">Why a checklist item is vague in the demand; <see langword="null"/> when it is clear</param>
/// <param name="Concluido">Done (checklist), tested (test), resolved (question) or respected (rule)</param>
/// <param name="ConcluidoEm">Local date and time of the marking; <see langword="null"/> when not marked</param>
/// <param name="Origem">
/// Where a rule came from (<c>descrição</c> or <c>comentário de Maria em 04/11/2026 14:30</c>);
/// <see langword="null"/> for the other types
/// </param>
public sealed record AnaliseItemResponse(
    Guid Identifier,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<TipoItemAnalise>))] TipoItemAnalise Tipo,
    short Ordem,
    string Texto,
    string? Grupo,
    string? Vago,
    bool Concluido,
    DateTime? ConcluidoEm,
    string? Origem);

/// <summary>
/// Marking of an analysis item
/// </summary>
/// <param name="Concluido"><see langword="true"/> marks the item as done/tested/resolved; <see langword="false"/> unmarks it</param>
public sealed record MarcarItemRequest(
    [property: Required(ErrorMessage = "Informe se o item está concluído.")] bool? Concluido);
