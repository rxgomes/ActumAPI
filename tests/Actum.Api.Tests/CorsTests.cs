using System.Net;
using Actum.Tests.Shared;
using Microsoft.Extensions.Hosting;

namespace Actum.Api.Tests;

/// <summary>
/// Cenários da capability <c>cross-origin-access</c>.
/// </summary>
public sealed class CorsTests(PostgresFixture postgres)
{
    private const string OrigemDoFront = "http://localhost:5173";

    private const string OrigemDesconhecida = "https://site-desconhecido.com";

    [Fact]
    public async Task OrigemPermitidaRecebeAllowOriginAsync()
    {
        using var resposta = await EnviarAsync(Environments.Development, Requisicao(HttpMethod.Get, OrigemDoFront));

        Assert.Equal(OrigemDoFront, Cabecalho(resposta, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task OrigemNaoPermitidaNaoRecebeAllowOriginAsync()
    {
        using var resposta = await EnviarAsync(Environments.Development, Requisicao(HttpMethod.Get, OrigemDesconhecida));

        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task PreflightDeOrigemPermitidaLiberaMetodoECabecalhoAsync()
    {
        using var resposta = await EnviarAsync(Environments.Development, Preflight(OrigemDoFront));

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(OrigemDoFront, Cabecalho(resposta, "Access-Control-Allow-Origin"));
        Assert.Contains("POST", Cabecalho(resposta, "Access-Control-Allow-Methods"));
        Assert.Contains("content-type", Cabecalho(resposta, "Access-Control-Allow-Headers"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreflightDeOrigemNaoPermitidaNaoLiberaAsync()
    {
        using var resposta = await EnviarAsync(Environments.Development, Preflight(OrigemDesconhecida));

        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ProductionSemConfiguracaoNaoLiberaNenhumaOrigemAsync()
    {
        using var resposta = await EnviarAsync(Environments.Production, Requisicao(HttpMethod.Get, OrigemDoFront));

        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task OrigemConfiguradaEmProductionEPermitidaAsync()
    {
        var settings = new Dictionary<string, string> { ["Cors:AllowedOrigins:0"] = "https://app.actum.com.br" };

        using var resposta = await EnviarAsync(Environments.Production, Requisicao(HttpMethod.Get, "https://app.actum.com.br"), settings);

        Assert.Equal("https://app.actum.com.br", Cabecalho(resposta, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task OrigemPermitidaNaoRecebeAllowCredentialsAsync()
    {
        using var resposta = await EnviarAsync(Environments.Development, Requisicao(HttpMethod.Get, OrigemDoFront));
        using var preflight = await EnviarAsync(Environments.Development, Preflight(OrigemDoFront));

        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Credentials"));
        Assert.Null(Cabecalho(preflight, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public void CuringaNaConfiguracaoImpedeInicializacao()
    {
        var settings = new Dictionary<string, string> { ["Cors:AllowedOrigins:0"] = "*" };
        using var factory = new ActumApiFactory(Environments.Production, postgres.ConnectionString, settings);

        var erro = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("curinga", erro.Message);
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        string ambiente,
        HttpRequestMessage requisicao,
        IReadOnlyDictionary<string, string>? settings = null)
    {
        await using var factory = new ActumApiFactory(ambiente, postgres.ConnectionString, settings);
        using var cliente = factory.CreateClient();

        using (requisicao)
        {
            return await cliente.SendAsync(requisicao, TestContext.Current.CancellationToken);
        }
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string origem)
    {
        var requisicao = new HttpRequestMessage(metodo, "/health/live");
        requisicao.Headers.Add("Origin", origem);
        return requisicao;
    }

    private static HttpRequestMessage Preflight(string origem)
    {
        var requisicao = Requisicao(HttpMethod.Options, origem);
        requisicao.Headers.Add("Access-Control-Request-Method", "POST");
        requisicao.Headers.Add("Access-Control-Request-Headers", "content-type");
        return requisicao;
    }

    private static string? Cabecalho(HttpResponseMessage resposta, string nome)
    {
        return resposta.Headers.TryGetValues(nome, out var valores) ? string.Join(",", valores) : null;
    }
}
