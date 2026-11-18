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
/// <param name="Fontes">Sources sent to the AI; <see langword="null"/> for versions analyzed before the sources were stored</param>
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
    DateTime AnalisadaEm,
    FontesDaAnaliseResponse? Fontes);

/// <summary>
/// Sources of an analysis. Without a body, the analysis uses the description and the comments.
/// </summary>
/// <param name="Modo">
/// <c>tarefa</c> (description, comments and/or attachments of the task) or <c>prompt</c> (request written by the
/// user, in place of the description and the attachments); default <c>tarefa</c>
/// </param>
/// <param name="Descricao">Send the description (only in the <c>tarefa</c> mode); default <see langword="true"/></param>
/// <param name="Comentarios">
/// Send the comments and the reopenings; default <see langword="true"/> in the <c>tarefa</c> mode and
/// <see langword="false"/> in the <c>prompt</c> mode
/// </param>
/// <param name="Anexos">
/// Identifiers (<c>gid</c>) of attachments of the task in the Asana, in order: the chosen ones in the <c>tarefa</c>
/// mode, the ones cited by the prompt in the <c>prompt</c> mode
/// </param>
/// <param name="Prompt">Request written by the user, required in the <c>prompt</c> mode; it may cite files as <c>@name</c></param>
public sealed record FontesDaAnaliseRequest(
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<ModoDaAnalise>))] ModoDaAnalise? Modo = null,
    bool? Descricao = null,
    bool? Comentarios = null,
    IReadOnlyList<string>? Anexos = null,
    [property: StringLength(Analise.PromptMaxLength, ErrorMessage = "O prompt deve ter no máximo {1} caracteres.")] string? Prompt = null);

/// <summary>
/// Sources sent to the AI by one version of the analysis
/// </summary>
/// <param name="Modo"><c>tarefa</c> or <c>prompt</c></param>
/// <param name="Descricao">Whether the description was sent</param>
/// <param name="Comentarios">Whether the comments (and the reopenings) were sent</param>
/// <param name="Anexos">Attachments sent, in order</param>
/// <param name="Prompt">Request written by the user; <see langword="null"/> outside the <c>prompt</c> mode</param>
public sealed record FontesDaAnaliseResponse(
    [property: JsonConverter(typeof(SnakeCaseEnumJsonConverter<ModoDaAnalise>))] ModoDaAnalise Modo,
    bool Descricao,
    bool Comentarios,
    IReadOnlyList<AnexoDaFonteResponse> Anexos,
    string? Prompt);

/// <summary>
/// Attachment sent as a source of an analysis
/// </summary>
/// <param name="Gid">Attachment identifier in the Asana</param>
/// <param name="Nome">File name at the time of the analysis</param>
public sealed record AnexoDaFonteResponse(string Gid, string Nome);

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
/// Where a rule or the excerpt of a checklist item or question came from (<c>título</c>, <c>descrição</c>,
/// <c>comentário de Maria em 04/11/2026 14:30</c>, <c>anexo especificacao.pdf, página 3</c> or <c>prompt</c>);
/// <see langword="null"/> for tests and for items analyzed before the origin existed
/// </param>
/// <param name="Trecho">
/// Literal excerpt of the demand that raised a checklist item or question; <see langword="null"/> for tests, rules,
/// excerpts not informed and items analyzed before the excerpt existed
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
    string? Origem,
    string? Trecho);

/// <summary>
/// Marking of an analysis item
/// </summary>
/// <param name="Concluido"><see langword="true"/> marks the item as done/tested/resolved; <see langword="false"/> unmarks it</param>
public sealed record MarcarItemRequest(
    [property: Required(ErrorMessage = "Informe se o item está concluído.")] bool? Concluido);
