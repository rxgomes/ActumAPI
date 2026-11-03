using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Actum.Modules.Claude.Contracts;
using Actum.Modules.Claude.Errors;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Claude.Application;

/// <summary>
/// <see cref="IAnalisadorDeDemanda"/> over the official Anthropic SDK: one non-streaming call with structured
/// output (JSON Schema), explicit effort and server-side fallback on policy refusals. The stop reason is checked
/// before the content and SDK failures are translated into <see cref="ClaudeException"/>.
/// </summary>
/// <remarks>
/// Never logs the key, the prompt or the answer: only model, tokens and duration. The typed
/// <see cref="HttpClient"/> is registered with every header value redacted (the key travels in the <c>Authorization</c> header).
/// </remarks>
internal sealed class AnalisadorDeDemanda(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<AnalisadorDeDemanda> logger) : IAnalisadorDeDemanda
{
    /// <summary>
    /// Beta header of the <c>fallbacks: "default"</c> form
    /// </summary>
    public const string BetaDoFallback = "server-side-fallback-2026-07-01";

    /// <summary>
    /// Time limit of one attempt (thinking can take a while)
    /// </summary>
    public static readonly TimeSpan TempoLimite = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Group of a checklist item that came without one
    /// </summary>
    public const string GrupoPadrao = "Geral";

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The SDK controls the time limit of each attempt; the <see cref="HttpClient"/> must not cut it earlier
    /// </summary>
    public static void Configurar(HttpClient cliente)
    {
        cliente.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<DemandaEstruturada> AnalisarAsync(DemandaParaAnalise demanda, CancellationToken cancellationToken)
    {
        var opcoes = ClaudeOptions.Ler(configuration);
        if (!opcoes.Configurado)
        {
            throw new ClaudeNaoConfiguradoException();
        }

        var cliente = new AnthropicClient
        {
            ApiKey = opcoes.ApiKey,
            HttpClient = httpClient,
            MaxRetries = opcoes.NovasTentativas,
            Timeout = TempoLimite,
        };

        var parametros = new MessageCreateParams
        {
            Model = opcoes.Modelo,
            MaxTokens = opcoes.MaxTokens,
            Betas = [BetaDoFallback],
            Fallbacks = new Default(),
            System = PromptDaDemanda.Sistema,
            OutputConfig = new BetaOutputConfig
            {
                Effort = Esforco(opcoes.Esforco),
                Format = new BetaJsonOutputFormat { Schema = PromptDaDemanda.Esquema },
            },
            Messages = [new BetaUserMessageParam { Content = PromptDaDemanda.MontarMensagem(demanda) }],
        };

        var cronometro = Stopwatch.StartNew();
        var resposta = await EnviarAsync(cliente, parametros, cancellationToken);

        var resultado = Interpretar(resposta);

        logger.LogInformation(
            "Demanda analisada pela Claude API: modelo {Modelo}, {TokensEntrada} tokens de entrada, {TokensSaida} de saída, {Duracao} ms",
            resultado.Modelo, resultado.TokensEntrada, resultado.TokensSaida, cronometro.ElapsedMilliseconds);

        return resultado;
    }

    /// <summary>
    /// Turn the Claude API answer into the structured demand, checking the stop reason before the content
    /// </summary>
    internal static DemandaEstruturada Interpretar(BetaMessage resposta)
    {
        var motivo = resposta.StopReason?.Raw();
        switch (motivo)
        {
            case "refusal":
                throw new ClaudeRecusaException();
            case "end_turn":
                break;
            default:
                // max_tokens, model_context_window_exceeded ou qualquer parada inesperada: resposta não confiável.
                throw new ClaudeRespostaIncompletaException();
        }

        var json = string.Concat(resposta.Content
            .Select(bloco => bloco.TryPickText(out var texto) ? texto.Text : null)
            .OfType<string>());

        RespostaJson? conteudo;
        try
        {
            conteudo = JsonSerializer.Deserialize<RespostaJson>(json, OpcoesJson);
        }
        catch (JsonException erro)
        {
            throw new ClaudeRespostaIncompletaException(erro);
        }

        var objetivo = conteudo?.Objetivo?.Trim();
        if (conteudo is null || string.IsNullOrEmpty(objetivo) || conteudo.Checklist is null || conteudo.Testes is null || conteudo.Duvidas is null)
        {
            throw new ClaudeRespostaIncompletaException();
        }

        var checklist = conteudo.Checklist
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Texto))
            .Select(item => new ItemDoChecklist(
                string.IsNullOrWhiteSpace(item!.Grupo) ? GrupoPadrao : item.Grupo.Trim(),
                item.Texto!.Trim(),
                string.IsNullOrWhiteSpace(item.Vago) ? null : item.Vago.Trim()))
            .ToList();

        return new DemandaEstruturada(
            objetivo,
            checklist,
            Limpar(conteudo.Testes),
            Limpar(conteudo.Duvidas),
            resposta.Model.Raw(),
            (int)resposta.Usage.InputTokens,
            (int)resposta.Usage.OutputTokens);
    }

    private async Task<BetaMessage> EnviarAsync(AnthropicClient cliente, MessageCreateParams parametros, CancellationToken cancellationToken)
    {
        try
        {
            return await cliente.Beta.Messages.Create(parametros, cancellationToken);
        }
        catch (AnthropicUnauthorizedException erro)
        {
            throw new ClaudeChaveInvalidaException(erro);
        }
        catch (AnthropicForbiddenException erro)
        {
            throw new ClaudeChaveInvalidaException(erro);
        }
        catch (AnthropicRateLimitException erro)
        {
            throw new ClaudeIndisponivelException(erro);
        }
        catch (Anthropic5xxException erro)
        {
            throw new ClaudeIndisponivelException(erro);
        }
        catch (AnthropicIOException erro)
        {
            throw new ClaudeIndisponivelException(erro);
        }
        catch (AnthropicApiException erro) when (SemCredito(erro))
        {
            throw new ClaudeSemCreditoException(erro);
        }
        catch (AnthropicApiException erro)
        {
            logger.LogError("A Claude API recusou a requisição de análise: {Status}", erro.StatusCode);
            throw new ClaudeIndisponivelException(erro);
        }
        catch (TaskCanceledException erro) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ClaudeIndisponivelException(erro);
        }
    }

    /// <summary>
    /// Whether the Anthropic refused the call for lack of credits (the SDK puts the response body in the message)
    /// </summary>
    private static bool SemCredito(AnthropicApiException erro) =>
        erro.Message.Contains("credit balance", StringComparison.OrdinalIgnoreCase)
        || erro.Message.Contains("billing_error", StringComparison.OrdinalIgnoreCase);

    private static Effort Esforco(string esforco) => esforco switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        "max" => Effort.Max,
        _ => Effort.Medium,
    };

    private static List<string> Limpar(IEnumerable<string?> itens) =>
        itens.Select(item => item?.Trim()).Where(item => !string.IsNullOrEmpty(item)).Select(item => item!).ToList();

    private sealed class RespostaJson
    {
        [JsonPropertyName("objetivo")]
        public string? Objetivo { get; init; }

        [JsonPropertyName("checklist")]
        public List<ItemJson?>? Checklist { get; init; }

        [JsonPropertyName("testes")]
        public List<string?>? Testes { get; init; }

        [JsonPropertyName("duvidas")]
        public List<string?>? Duvidas { get; init; }
    }

    private sealed class ItemJson
    {
        [JsonPropertyName("grupo")]
        public string? Grupo { get; init; }

        [JsonPropertyName("texto")]
        public string? Texto { get; init; }

        [JsonPropertyName("vago")]
        public string? Vago { get; init; }
    }
}
