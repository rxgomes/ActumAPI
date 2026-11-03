using System.Net;
using System.Text.Json;
using Actum.Modules.Asana.Tests;
using Actum.Tests.Shared;
using Microsoft.Extensions.Hosting;

namespace Actum.Api.Tests;

/// <summary>
/// Cenários da capability <c>asana-client</c> no host: <c>GET /asana/conexao</c> e a tradução das falhas do
/// Asana em Problem Details. O Asana é simulado pelo <see cref="HttpMessageHandlerFalso"/>.
/// </summary>
public sealed class AsanaApiTests(PostgresFixture postgres)
{
    private const string ProblemJson = "application/problem+json";

    private const string Token = "token-secreto-de-teste";

    private readonly HttpMessageHandlerFalso _asana = new();

    [Fact]
    public async Task ConexaoComTokenValidoTrazUsuarioEWorkspacesAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new
        {
            data = new
            {
                gid = "7",
                name = "Ricardo",
                workspaces = new[] { new { gid = "111", name = "Aureum-ERP" }, new { gid = "222", name = "aremas.com.br" } },
            },
        });

        var (status, corpo) = await ObterAsync("/asana/conexao", Token);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(corpo.GetProperty("configurado").GetBoolean());
        Assert.True(corpo.GetProperty("valido").GetBoolean());
        Assert.Equal("Ricardo", corpo.GetProperty("usuario").GetString());

        var workspaces = corpo.GetProperty("workspaces").EnumerateArray()
            .Select(workspace => (workspace.GetProperty("gid").GetString(), workspace.GetProperty("nome").GetString()));
        Assert.Equal([("111", "Aureum-ERP"), ("222", "aremas.com.br")], workspaces);

        Assert.Equal(Token, Assert.Single(_asana.Requisicoes).Autorizacao?.Parameter);
    }

    [Fact]
    public async Task ConexaoComTokenInvalidoRespondeValidoFalseAsync()
    {
        _asana.Responder(HttpStatusCode.Unauthorized, new { errors = new[] { new { message = "Not Authorized" } } });

        var (status, corpo) = await ObterAsync("/asana/conexao", Token);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(corpo.GetProperty("configurado").GetBoolean());
        Assert.False(corpo.GetProperty("valido").GetBoolean());
        Assert.DoesNotContain(Token, corpo.GetRawText());
    }

    [Fact]
    public async Task ConexaoSemTokenRespondeConfiguradoFalseAsync()
    {
        var (status, corpo) = await ObterAsync("/asana/conexao", token: string.Empty);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(corpo.GetProperty("configurado").GetBoolean());
        Assert.Empty(_asana.Requisicoes);
    }

    [Theory]
    [InlineData("/__testes/asana")]
    [InlineData("/asana/conexao")]
    public async Task AsanaForaDoArRetorna502Async(string caminho)
    {
        _asana.Responder(HttpStatusCode.ServiceUnavailable);

        var (status, corpo, tipo) = await ObterComTipoAsync(caminho, Token);

        Assert.Equal(HttpStatusCode.BadGateway, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal(502, corpo.GetProperty("status").GetInt32());
        Assert.Equal("Falha na comunicação com um serviço externo.", corpo.GetProperty("title").GetString());
        Assert.Contains("Não foi possível comunicar com o Asana", corpo.GetProperty("detail").GetString());
        Assert.DoesNotContain(Token, corpo.GetRawText());
    }

    [Fact]
    public async Task TokenRevogadoRetorna400Async()
    {
        _asana.Responder(HttpStatusCode.Unauthorized, new { errors = new[] { new { message = "Not Authorized" } } });

        var (status, corpo, tipo) = await ObterComTipoAsync("/__testes/asana", Token);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("Requisição inválida.", corpo.GetProperty("title").GetString());
        Assert.Contains("token do Asana é inválido", corpo.GetProperty("detail").GetString());
        Assert.DoesNotContain(Token, corpo.GetRawText());
    }

    [Fact]
    public async Task TokenNaoConfiguradoEmEndpointDependenteRetorna400Async()
    {
        var (status, corpo, tipo) = await ObterComTipoAsync("/__testes/asana", token: string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Contains("não está configurado", corpo.GetProperty("detail").GetString());
        Assert.Empty(_asana.Requisicoes);
    }

    [Fact]
    public async Task DocumentoOpenApiContemConexaoComTagAsanaAsync()
    {
        await using var factory = new ActumApiFactory(Environments.Development, postgres.ConnectionString, asana: _asana);
        using var cliente = factory.CreateClient();

        using var resposta = await cliente.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using var documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var operacao = documento.RootElement.GetProperty("paths").GetProperty("/asana/conexao").GetProperty("get");
        Assert.Equal(["Asana"], operacao.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()));
        Assert.Equal("VerificarConexaoAsana", operacao.GetProperty("operationId").GetString());
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> ObterAsync(string caminho, string token)
    {
        var (status, corpo, _) = await ObterComTipoAsync(caminho, token);
        return (status, corpo);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, string? Tipo)> ObterComTipoAsync(string caminho, string token)
    {
        // Production: não carrega os user-secrets da máquina, então só vale o token informado aqui.
        var settings = new Dictionary<string, string> { ["Asana:Token"] = token };
        await using var factory = new ActumApiFactory(Environments.Production, postgres.ConnectionString, settings, asana: _asana);
        using var cliente = factory.CreateClient();

        using var resposta = await cliente.GetAsync(caminho, TestContext.Current.CancellationToken);
        var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone(), resposta.Content.Headers.ContentType?.MediaType);
    }
}
