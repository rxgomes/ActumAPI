using System.Net;
using System.Text.Json;
using Actum.Modules.GitHub.Application;
using Actum.Modules.GitHub.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Actum.Modules.GitHub.Tests;

/// <summary>
/// Cenários da capability <c>github-client</c> no cliente, sem rede.
/// </summary>
public sealed class GitHubClientTests
{
    private const string Token = "token-de-teste";

    private const string Repositorio = "ixact/erp-api";

    private const string Sha = "abc123";

    private readonly HttpMessageHandlerFalso _github = new();

    [Fact]
    public async Task RequisicaoLevaTokenECabecalhosAsync()
    {
        _github.Responder(HttpStatusCode.OK, Referencia("refs/heads/main"));

        var sha = await Criar().ObterShaDaBranchAsync(Repositorio, "main", TestContext.Current.CancellationToken);

        Assert.Equal(Sha, sha);
        var requisicao = Assert.Single(_github.Requisicoes);
        Assert.Equal(HttpMethod.Get, requisicao.Metodo);
        Assert.Equal("https://api.github.com/repos/ixact/erp-api/git/ref/heads/main", requisicao.Uri.ToString());
        Assert.Equal($"Bearer {Token}", requisicao.Cabecalhos["Authorization"]);
        Assert.Equal("application/vnd.github+json", requisicao.Cabecalhos["Accept"]);
        Assert.Equal(GitHubClient.VersaoDaApi, requisicao.Cabecalhos["X-GitHub-Api-Version"]);
        Assert.Equal(GitHubClient.AgenteDoUsuario, requisicao.Cabecalhos["User-Agent"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TokenAusenteFalhaSemChamarOGitHubAsync(string? token)
    {
        var erro = await Assert.ThrowsAsync<GitHubNaoConfiguradoException>(
            () => Criar(token).ObterShaDaBranchAsync(Repositorio, "main", TestContext.Current.CancellationToken));

        Assert.Contains("não está configurado", erro.Message);
        Assert.Empty(_github.Requisicoes);
    }

    [Fact]
    public async Task BranchComBarraNaoCodificaABarraAsync()
    {
        _github.Responder(HttpStatusCode.OK, Referencia("refs/heads/release/2026-11"));

        await Criar().ObterShaDaBranchAsync(Repositorio, "release/2026-11", TestContext.Current.CancellationToken);

        Assert.Equal(
            "/repos/ixact/erp-api/git/ref/heads/release/2026-11",
            Assert.Single(_github.Requisicoes).Uri.AbsolutePath);
    }

    [Fact]
    public async Task BranchInexistenteDevolveNuloAsync()
    {
        _github
            .Responder(HttpStatusCode.NotFound, new { message = "Not Found" })
            .Responder(HttpStatusCode.OK, new { full_name = Repositorio });

        var sha = await Criar().ObterShaDaBranchAsync(Repositorio, "release/2026-12", TestContext.Current.CancellationToken);

        Assert.Null(sha);
        Assert.Equal("https://api.github.com/repos/ixact/erp-api", _github.Requisicoes[1].Uri.ToString());
    }

    [Fact]
    public async Task RepositorioVazioDevolveNuloAsync()
    {
        _github
            .Responder(HttpStatusCode.Conflict, new { message = "Git Repository is empty." })
            .Responder(HttpStatusCode.OK, new { full_name = Repositorio });

        Assert.Null(await Criar().ObterShaDaBranchAsync(Repositorio, "main", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task RepositorioSemAcessoFalhaCitandoORepositorioAsync(HttpStatusCode status)
    {
        _github
            .Responder(HttpStatusCode.NotFound, new { message = "Not Found" })
            .Responder(status, new { message = "Not Found" });

        var erro = await Assert.ThrowsAsync<GitHubRepositorioInacessivelException>(
            () => Criar().ObterShaDaBranchAsync(Repositorio, "main", TestContext.Current.CancellationToken));

        Assert.Contains(Repositorio, erro.Message);
    }

    [Fact]
    public async Task BranchCriadaAsync()
    {
        _github.Responder(HttpStatusCode.Created, Referencia("refs/heads/261105-consumidor-final-nf"));

        var criada = await Criar().CriarBranchAsync(Repositorio, "261105-consumidor-final-nf", Sha, TestContext.Current.CancellationToken);

        Assert.True(criada);
        var requisicao = Assert.Single(_github.Requisicoes);
        Assert.Equal(HttpMethod.Post, requisicao.Metodo);
        Assert.Equal("https://api.github.com/repos/ixact/erp-api/git/refs", requisicao.Uri.ToString());

        using var corpo = JsonDocument.Parse(requisicao.Corpo!);
        Assert.Equal("refs/heads/261105-consumidor-final-nf", corpo.RootElement.GetProperty("ref").GetString());
        Assert.Equal(Sha, corpo.RootElement.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task BranchJaExistenteNaoEhErroAsync()
    {
        _github.Responder(HttpStatusCode.UnprocessableEntity, new { message = "Reference already exists" });

        var criada = await Criar().CriarBranchAsync(Repositorio, "261105-consumidor-final-nf", Sha, TestContext.Current.CancellationToken);

        Assert.False(criada);
    }

    [Fact]
    public async Task OutraRecusaNaCriacaoFalhaAsync()
    {
        _github.Responder(HttpStatusCode.UnprocessableEntity, new { message = "Reference update failed" });

        var erro = await Assert.ThrowsAsync<GitHubBranchRecusadaException>(
            () => Criar().CriarBranchAsync(Repositorio, "nova", Sha, TestContext.Current.CancellationToken));

        Assert.Contains(Repositorio, erro.Message);
    }

    [Fact]
    public async Task TokenRecusadoAsync()
    {
        _github.Responder(HttpStatusCode.Unauthorized, new { message = "Bad credentials" });

        var erro = await Assert.ThrowsAsync<GitHubTokenRecusadoException>(
            () => Criar().ObterShaDaBranchAsync(Repositorio, "main", TestContext.Current.CancellationToken));

        Assert.Contains("token do GitHub foi recusado", erro.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CriacaoSemPermissaoFalhaCitandoORepositorioAsync(HttpStatusCode status)
    {
        _github.Responder(status, new { message = "Resource not accessible by personal access token" });

        var erro = await Assert.ThrowsAsync<GitHubRepositorioInacessivelException>(
            () => Criar().CriarBranchAsync(Repositorio, "nova", Sha, TestContext.Current.CancellationToken));

        Assert.Contains(Repositorio, erro.Message);
    }

    [Fact]
    public async Task LimiteEsgotadoEhIndisponibilidadeAsync()
    {
        _github.Responder(HttpStatusCode.Forbidden, new { message = "API rate limit exceeded" }, new Dictionary<string, string> { ["X-RateLimit-Remaining"] = "0" });

        await Assert.ThrowsAsync<GitHubIndisponivelException>(
            () => Criar().ObterShaDaBranchAsync(Repositorio, "main", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ErroDoServidorEhIndisponibilidadeAsync(HttpStatusCode status)
    {
        _github.Responder(status);

        await Assert.ThrowsAsync<GitHubIndisponivelException>(
            () => Criar().CriarBranchAsync(Repositorio, "nova", Sha, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FalhaDeRedeEhIndisponibilidadeAsync()
    {
        _github.Falhar(new HttpRequestException("sem rede"));

        await Assert.ThrowsAsync<GitHubIndisponivelException>(
            () => Criar().ObterShaDaBranchAsync(Repositorio, "main", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TempoEsgotadoEhIndisponibilidadeAsync()
    {
        _github.Falhar(new TaskCanceledException("tempo esgotado"));

        await Assert.ThrowsAsync<GitHubIndisponivelException>(
            () => Criar().ObterShaDaBranchAsync(Repositorio, "main", TestContext.Current.CancellationToken));
    }

    private GitHubClient Criar(string? token = Token)
    {
        var httpClient = new HttpClient(_github);
        GitHubClient.Configurar(httpClient);

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [GitHubClient.TokenKey] = token })
            .Build();

        return new GitHubClient(httpClient, configuracao, NullLogger<GitHubClient>.Instance);
    }

    private static object Referencia(string nome)
    {
        return new { @ref = nome, @object = new { sha = Sha, type = "commit" } };
    }
}
