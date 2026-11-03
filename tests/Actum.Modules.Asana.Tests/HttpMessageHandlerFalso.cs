using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Actum.Modules.Asana.Tests;

/// <summary>
/// <see cref="HttpMessageHandler"/> de teste: devolve as respostas roteirizadas na ordem em que foram
/// enfileiradas e registra cada requisição recebida. Requisição sem resposta roteirizada falha o teste,
/// para nenhuma chamada ir à rede.
/// </summary>
public sealed class HttpMessageHandlerFalso : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _respostas = new();

    private readonly List<RequisicaoRegistrada> _requisicoes = [];

    private readonly Lock _trava = new();

    /// <summary>
    /// Requisições recebidas, na ordem
    /// </summary>
    public IReadOnlyList<RequisicaoRegistrada> Requisicoes
    {
        get
        {
            lock (_trava)
            {
                return [.. _requisicoes];
            }
        }
    }

    /// <summary>
    /// Enfileira uma resposta com o corpo serializado em JSON (nomes de propriedade como escritos)
    /// </summary>
    public HttpMessageHandlerFalso Responder(HttpStatusCode status, object? corpo = null, TimeSpan? retryAfter = null)
    {
        lock (_trava)
        {
            _respostas.Enqueue(() =>
            {
                var resposta = new HttpResponseMessage(status);
                if (corpo is not null)
                {
                    resposta.Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");
                }

                if (retryAfter is { } espera)
                {
                    resposta.Headers.RetryAfter = new RetryConditionHeaderValue(espera);
                }

                return resposta;
            });
        }

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        lock (_trava)
        {
            _requisicoes.Add(new RequisicaoRegistrada(request.Method, request.RequestUri!, request.Headers.Authorization, corpo));

            if (!_respostas.TryDequeue(out var criarResposta))
            {
                throw new InvalidOperationException($"Requisição não roteirizada: {request.Method} {request.RequestUri}");
            }

            var resposta = criarResposta();
            resposta.RequestMessage = request;
            return resposta;
        }
    }
}

/// <summary>
/// Requisição recebida pelo <see cref="HttpMessageHandlerFalso"/>
/// </summary>
public sealed record RequisicaoRegistrada(HttpMethod Metodo, Uri Uri, AuthenticationHeaderValue? Autorizacao, string? Corpo);
