namespace Actum.Modules.Claude.Contracts;

/// <summary>
/// Everything the AI reads about a task to structure its demand
/// </summary>
/// <param name="Empresa">Company (Asana workspace) name</param>
/// <param name="Titulo">Task title</param>
/// <param name="Descricao">Task description in plain text</param>
/// <param name="Projetos">Asana project names, comma separated</param>
/// <param name="Comentarios">Comments in chronological order</param>
/// <param name="Retrabalhos">Reopenings in order (empty when never reopened)</param>
public sealed record DemandaParaAnalise(
    string Empresa,
    string Titulo,
    string Descricao,
    string Projetos,
    IReadOnlyList<ComentarioDaDemanda> Comentarios,
    IReadOnlyList<RetrabalhoDaDemanda> Retrabalhos);

/// <summary>
/// Comment of the task
/// </summary>
/// <param name="Autor">Author name; <see langword="null"/> when unknown</param>
/// <param name="CriadoEm">Local date and time of the comment</param>
/// <param name="Texto">Comment text</param>
public sealed record ComentarioDaDemanda(string? Autor, DateTime CriadoEm, string Texto);

/// <summary>
/// Reopening (rework) of the task
/// </summary>
/// <param name="Numero">Sequence of the rework, starting at 1</param>
/// <param name="ReabertaEm">Local date and time of the reopening</param>
/// <param name="ReabertaPor">Who reopened it; <see langword="null"/> when unknown</param>
public sealed record RetrabalhoDaDemanda(short Numero, DateTime ReabertaEm, string? ReabertaPor);

/// <summary>
/// Demand structured by the AI
/// </summary>
/// <param name="Objetivo">Expected result, in a few sentences</param>
/// <param name="Checklist">Verifiable actions, grouped by area, in execution order</param>
/// <param name="Testes">How to validate the delivery (test cases and reference data)</param>
/// <param name="Duvidas">Open questions to the requester</param>
/// <param name="Regras">Business rules, constraints and suggestions to respect, each with where it came from</param>
/// <param name="Modelo">Model that actually answered</param>
/// <param name="TokensEntrada">Input tokens consumed</param>
/// <param name="TokensSaida">Output tokens consumed</param>
public sealed record DemandaEstruturada(
    string Objetivo,
    IReadOnlyList<ItemDoChecklist> Checklist,
    IReadOnlyList<string> Testes,
    IReadOnlyList<string> Duvidas,
    IReadOnlyList<RegraDaDemanda> Regras,
    string Modelo,
    int TokensEntrada,
    int TokensSaida);

/// <summary>
/// Checklist action
/// </summary>
/// <param name="Grupo">Area of the action (e.g. <c>Front</c>, <c>Back: cálculo</c>)</param>
/// <param name="Texto">Action to do</param>
/// <param name="Vago">Why the action is vague in the demand; <see langword="null"/> when it is clear</param>
public sealed record ItemDoChecklist(string Grupo, string Texto, string? Vago);

/// <summary>
/// Rule, constraint or suggestion of the demand
/// </summary>
/// <param name="Texto">What must be respected or considered</param>
/// <param name="Origem">
/// Where it came from: <c>descrição</c> or <c>comentário de &lt;autor&gt; em &lt;dd/MM/yyyy HH:mm&gt;</c>
/// </param>
public sealed record RegraDaDemanda(string Texto, string Origem);
