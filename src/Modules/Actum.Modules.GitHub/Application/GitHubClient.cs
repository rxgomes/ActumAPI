using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Actum.Modules.GitHub.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.GitHub.Application;

/// <summary>
/// <see cref="IGitHubClient"/> over the GitHub REST API (<c>https://api.github.com</c>), authenticated by the token
/// in <c>GitHub:Token</c>, translating failures into <see cref="GitHubException"/>.
/// </summary>
/// <remarks>
/// The token is read on every operation (a token configured later is picked up without restart) and is never
/// logged: the module registers the typed <see cref="HttpClient"/> with every header value redacted.
/// </remarks>
internal sealed class GitHubClient(HttpClient httpClient, IConfiguration configuration, ILogger<GitHubClient> logger) : IGitHubClient
{
    public const string TokenKey = "GitHub:Token";

    public const string VersaoDaApi = "2022-11-28";

    public const string AgenteDoUsuario = "Actum";

    public static readonly Uri EnderecoBase = new("https://api.github.com/");

    public static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(30);

    private const string ReferenciaJaExiste = "Reference already exists";

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Base address and fixed headers of the GitHub <see cref="HttpClient"/>. The time limit is set per call
    /// (<see cref="TempoLimite"/>), so the client itself has none.
    /// </summary>
    public static void Configurar(HttpClient cliente)
    {
        cliente.BaseAddress = EnderecoBase;
        cliente.Timeout = Timeout.InfiniteTimeSpan;
        cliente.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AgenteDoUsuario, null));
        cliente.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        cliente.DefaultRequestHeaders.Add("X-GitHub-Api-Version", VersaoDaApi);
    }

    public async Task<string?> ObterShaDaBranchAsync(string repositorio, string branch, CancellationToken cancellationToken)
    {
        using var resposta = await EnviarAsync(HttpMethod.Get, $"repos/{Repositorio(repositorio)}/git/ref/heads/{Caminho(branch)}", null, cancellationToken);

        // 409: repositório vazio, sem nenhuma branch.
        if (resposta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            // O 404 da referência não diz se faltou a branch ou o repositório: o repositório é consultado à parte.
            await GarantirRepositorioAcessivelAsync(repositorio, cancellationToken);
            return null;
        }

        GarantirSucesso(resposta, repositorio);

        var referencia = await LerAsync<ReferenciaJson>(resposta, cancellationToken);
        if (string.IsNullOrEmpty(referencia?.Object?.Sha))
        {
            throw new GitHubIndisponivelException();
        }

        return referencia.Object.Sha;
    }

    public async Task<bool> CriarBranchAsync(string repositorio, string branch, string sha, CancellationToken cancellationToken)
    {
        var corpo = new { @ref = $"refs/heads/{branch}", sha };
        using var resposta = await EnviarAsync(HttpMethod.Post, $"repos/{Repositorio(repositorio)}/git/refs", corpo, cancellationToken);

        if (resposta.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var erro = await LerAsync<ErroJson>(resposta, cancellationToken);
            if (erro?.Message?.Contains(ReferenciaJaExiste, StringComparison.OrdinalIgnoreCase) == true)
            {
                return false;
            }

            logger.LogWarning("GitHub recusou a criação da branch em {Repositorio}: {Mensagem}", repositorio, erro?.Message);
            throw new GitHubBranchRecusadaException(repositorio, branch);
        }

        GarantirSucesso(resposta, repositorio);
        return true;
    }

    private async Task GarantirRepositorioAcessivelAsync(string repositorio, CancellationToken cancellationToken)
    {
        using var resposta = await EnviarAsync(HttpMethod.Get, $"repos/{Repositorio(repositorio)}", null, cancellationToken);
        GarantirSucesso(resposta, repositorio);
    }

    /// <summary>
    /// Send the request with the token and its own time limit. Network failures and timeouts become
    /// <see cref="GitHubIndisponivelException"/>; the status is checked by the caller.
    /// </summary>
    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string caminho, object? corpo, CancellationToken cancellationToken)
    {
        var token = configuration[TokenKey];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new GitHubNaoConfiguradoException();
        }

        using var requisicao = new HttpRequestMessage(metodo, caminho);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (corpo is not null)
        {
            requisicao.Content = JsonContent.Create(corpo, options: OpcoesJson);
        }

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(TempoLimite);

        try
        {
            var resposta = await httpClient.SendAsync(requisicao, limite.Token);

            // O conteúdo é lido ainda dentro do tempo limite, para a resposta não ficar presa à conexão.
            await resposta.Content.LoadIntoBufferAsync(limite.Token);
            return resposta;
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubIndisponivelException(ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Tempo limite da chamada.
            throw new GitHubIndisponivelException(ex);
        }
    }

    private void GarantirSucesso(HttpResponseMessage resposta, string repositorio)
    {
        if (resposta.IsSuccessStatusCode)
        {
            return;
        }

        var caminho = resposta.RequestMessage?.RequestUri?.AbsolutePath;

        if (resposta.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogWarning("GitHub recusou o token: {Caminho}", caminho);
            throw new GitHubTokenRecusadoException();
        }

        if (resposta.StatusCode == HttpStatusCode.Forbidden && LimiteEsgotado(resposta))
        {
            logger.LogWarning("Limite de requisições do GitHub esgotado: {Caminho}", caminho);
            throw new GitHubIndisponivelException();
        }

        if (resposta.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            logger.LogWarning("GitHub negou acesso ({Status}) ao repositório {Repositorio}", (int)resposta.StatusCode, repositorio);
            throw new GitHubRepositorioInacessivelException(repositorio);
        }

        logger.LogWarning("GitHub respondeu {Status}: {Caminho}", (int)resposta.StatusCode, caminho);
        throw new GitHubIndisponivelException();
    }

    private static bool LimiteEsgotado(HttpResponseMessage resposta)
    {
        return resposta.Headers.TryGetValues("X-RateLimit-Remaining", out var valores)
            && valores.FirstOrDefault() == "0";
    }

    private static async Task<T?> LerAsync<T>(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        try
        {
            return await resposta.Content.ReadFromJsonAsync<T>(OpcoesJson, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new GitHubIndisponivelException(ex);
        }
    }

    /// <summary>
    /// <c>owner/repo</c> with each part escaped
    /// </summary>
    private static string Repositorio(string repositorio)
    {
        return Caminho(repositorio);
    }

    /// <summary>
    /// Escape each segment, keeping the <c>/</c> between them (branch names such as <c>release/2026-11</c>)
    /// </summary>
    private static string Caminho(string valor)
    {
        return string.Join('/', valor.Split('/').Select(Uri.EscapeDataString));
    }

    private sealed record ReferenciaJson(string? Ref, ObjetoJson? Object);

    private sealed record ObjetoJson(string? Sha);

    private sealed record ErroJson(string? Message);
}
