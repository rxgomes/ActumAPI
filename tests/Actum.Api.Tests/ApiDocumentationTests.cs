using System.Net;
using System.Text.Json;
using Actum.Tests.Shared;
using Microsoft.Extensions.Hosting;

namespace Actum.Api.Tests;

/// <summary>
/// Cenários da capability <c>api-documentation</c>.
/// </summary>
public sealed class ApiDocumentationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task DocumentoOpenApiDisponivelEmDevelopmentAsync()
    {
        await using var factory = new ActumApiFactory(Environments.Development, postgres.ConnectionString);
        using var cliente = factory.CreateClient();

        using var resposta = await cliente.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);

        using var documento = await LerJsonAsync(resposta);
        Assert.True(documento.RootElement.TryGetProperty("openapi", out _));
        Assert.Equal("Actum API", documento.RootElement.GetProperty("info").GetProperty("title").GetString());
    }

    [Fact]
    public async Task DocumentoNaoContemEndpointsDeHealthCheckAsync()
    {
        await using var factory = new ActumApiFactory(Environments.Development, postgres.ConnectionString);
        using var cliente = factory.CreateClient();

        using var resposta = await cliente.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var documento = await LerJsonAsync(resposta);

        var caminhos = documento.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        Assert.DoesNotContain("/health/live", caminhos);
        Assert.DoesNotContain("/health/ready", caminhos);
    }

    [Fact]
    public async Task InterfaceDeDocumentacaoDisponivelEmDevelopmentAsync()
    {
        await using var factory = new ActumApiFactory(Environments.Development, postgres.ConnectionString);
        using var cliente = factory.CreateClient();

        using var resposta = await cliente.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);
        var corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        // O Scalar referencia o documento com URL relativa ("openapi/v1.json").
        Assert.Contains("\"url\":\"openapi/v1.json\"", corpo);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task DocumentacaoNaoExpostaEmProductionAsync(string caminho)
    {
        await using var factory = new ActumApiFactory(Environments.Production, postgres.ConnectionString);
        using var cliente = factory.CreateClient();

        using var resposta = await cliente.GetAsync(caminho, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    private static async Task<JsonDocument> LerJsonAsync(HttpResponseMessage resposta)
    {
        await using var conteudo = await resposta.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        return await JsonDocument.ParseAsync(conteudo, cancellationToken: TestContext.Current.CancellationToken);
    }
}
