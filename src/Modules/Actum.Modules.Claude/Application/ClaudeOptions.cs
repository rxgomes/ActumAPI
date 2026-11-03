using Microsoft.Extensions.Configuration;

namespace Actum.Modules.Claude.Application;

/// <summary>
/// Settings of the <c>Claude</c> configuration section, with defaults. Read on every analysis, so a key
/// configured later is picked up without restart. The key is never logged nor returned.
/// </summary>
/// <param name="ApiKey">Anthropic API key; <see langword="null"/> when not configured</param>
/// <param name="Modelo">Model id</param>
/// <param name="Esforco">Effort (<c>low</c>, <c>medium</c>, <c>high</c> or <c>max</c>)</param>
/// <param name="MaxTokens">Output token limit</param>
/// <param name="NovasTentativas">Retries made by the SDK on transient failures</param>
internal sealed record ClaudeOptions(string? ApiKey, string Modelo, string Esforco, int MaxTokens, int NovasTentativas)
{
    public const string Secao = "Claude";

    public const string ModeloPadrao = "claude-opus-5-5";

    public const string EsforcoPadrao = "medium";

    public const int MaxTokensPadrao = 16000;

    public const int NovasTentativasPadrao = 2;

    /// <summary>
    /// Whether the API key is configured
    /// </summary>
    public bool Configurado => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Read the <c>Claude</c> section, applying the defaults
    /// </summary>
    public static ClaudeOptions Ler(IConfiguration configuration)
    {
        var secao = configuration.GetSection(Secao);

        return new ClaudeOptions(
            secao["ApiKey"],
            Texto(secao["Modelo"], ModeloPadrao),
            Texto(secao["Esforco"], EsforcoPadrao).ToLowerInvariant(),
            int.TryParse(secao["MaxTokens"], out var maxTokens) && maxTokens > 0 ? maxTokens : MaxTokensPadrao,
            int.TryParse(secao["NovasTentativas"], out var novasTentativas) && novasTentativas >= 0 ? novasTentativas : NovasTentativasPadrao);
    }

    /// <summary>
    /// Never print the key (records print every property by default)
    /// </summary>
    public override string ToString() => $"ClaudeOptions {{ Configurado = {Configurado}, Modelo = {Modelo}, Esforco = {Esforco} }}";

    private static string Texto(string? valor, string padrao) => string.IsNullOrWhiteSpace(valor) ? padrao : valor.Trim();
}
