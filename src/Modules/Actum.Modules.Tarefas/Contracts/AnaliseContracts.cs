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
/// Checklist item, test or question of an analysis
/// </summary>
/// <param name="Identifier">Item identifier</param>
/// <param name="Tipo"><c>checklist</c>, <c>teste</c> or <c>duvida</c></param>
/// <param name="Ordem">Position among the items of the same type, starting at 1</param>
/// <param name="Texto">Item text</param>
/// <param name="Grupo">Area of a checklist item; <see langword="null"/> for tests and questions</param>
/// <param name="Vago">Why a checklist item is vague in the demand; <see langword="null"/> when it is clear</param>
/// <param name="Concluido">Done (checklist), tested (test) or resolved (question)</param>
/// <param name="ConcluidoEm">Local date and time of the marking; <see langword="null"/> when not marked</param>
public sealed record AnaliseItemResponse(
    Guid Identifier,
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<TipoItemAnalise>))] TipoItemAnalise Tipo,
    short Ordem,
    string Texto,
    string? Grupo,
    string? Vago,
    bool Concluido,
    DateTime? ConcluidoEm);

/// <summary>
/// Marking of an analysis item
/// </summary>
/// <param name="Concluido"><see langword="true"/> marks the item as done/tested/resolved; <see langword="false"/> unmarks it</param>
public sealed record MarcarItemRequest(
    [property: Required(ErrorMessage = "Informe se o item está concluído.")] bool? Concluido);
