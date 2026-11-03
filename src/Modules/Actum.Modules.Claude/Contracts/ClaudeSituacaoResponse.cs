namespace Actum.Modules.Claude.Contracts;

/// <summary>
/// Configuration state of the Claude API (the key itself is never returned)
/// </summary>
/// <param name="Configurado">Whether <c>Claude:ApiKey</c> is configured</param>
/// <param name="Modelo">Model used in the analyses</param>
public sealed record ClaudeSituacaoResponse(bool Configurado, string Modelo);
