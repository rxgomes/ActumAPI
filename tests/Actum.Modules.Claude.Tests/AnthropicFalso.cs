using System.Net;
using System.Text;
using System.Text.Json;

namespace Actum.Modules.Claude.Tests;

/// <summary>
/// <see cref="HttpMessageHandler"/> de teste que faz o papel da Claude API: devolve as respostas roteirizadas
/// na ordem e guarda o corpo de cada requisição. Requisição sem resposta roteirizada falha o teste, para
/// nenhuma chamada ir à rede (nem ser cobrada).
/// </summary>
public sealed class AnthropicFalso : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _respostas = new();

    private readonly List<(HttpRequestMessage Requisicao, string Corpo)> _requisicoes = [];

    /// <summary>
    /// Requisições recebidas (com o corpo lido), na ordem
    /// </summary>
    public IReadOnlyList<(HttpRequestMessage Requisicao, string Corpo)> Requisicoes => _requisicoes;

    /// <summary>
    /// Enfileira uma mensagem de sucesso com o texto e o motivo de parada informados
    /// </summary>
    public AnthropicFalso ResponderMensagem(string texto, string stopReason = "end_turn", string modelo = "claude-opus-5-5")
    {
        var corpo = new
        {
            id = "msg_teste",
            type = "message",
            role = "assistant",
            model = modelo,
            content = new object[] { new { type = "text", text = texto } },
            stop_reason = stopReason,
            stop_sequence = (string?)null,
            usage = new { input_tokens = 1200, output_tokens = 345 },
        };

        return Responder(HttpStatusCode.OK, corpo);
    }

    /// <summary>
    /// Enfileira o JSON estruturado esperado como texto da mensagem
    /// </summary>
    public AnthropicFalso ResponderEstruturado(string objetivo, string[] checklist, string[] duvidas) =>
        ResponderMensagem(JsonSerializer.Serialize(new
        {
            objetivo,
            checklist = checklist.Select(texto => new { grupo = "Geral", texto, vago = "" }),
            testes = Array.Empty<string>(),
            duvidas,
        }));

    /// <summary>
    /// Enfileira um erro da API no formato da Anthropic
    /// </summary>
    public AnthropicFalso ResponderErro(HttpStatusCode status, string tipo = "api_error") =>
        Responder(status, new { type = "error", error = new { type = tipo, message = "erro de teste" } });

    /// <summary>
    /// Enfileira uma resposta arbitrária
    /// </summary>
    public AnthropicFalso Responder(HttpStatusCode status, object corpo)
    {
        _respostas.Enqueue(() => new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json"),
        });

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var corpo = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        _requisicoes.Add((request, corpo));

        if (!_respostas.TryDequeue(out var resposta))
        {
            throw new InvalidOperationException($"Requisição sem resposta roteirizada: {request.Method} {request.RequestUri}");
        }

        return resposta();
    }
}
