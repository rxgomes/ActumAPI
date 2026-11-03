using System.Net;
using Actum.Tests.Shared;
using Microsoft.Extensions.Hosting;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Actum.Api.Tests;

/// <summary>
/// Cenários da capability <c>health-check</c>.
/// </summary>
public sealed class HealthCheckTests(PostgresFixture postgres)
{
    [Fact]
    public async Task LiveRetornaHealthyAsync()
    {
        var (status, corpo) = await ObterAsync(postgres.ConnectionString, "/health/live");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", corpo);
    }

    [Fact]
    public async Task LiveContinuaHealthyComBancoInacessivelAsync()
    {
        var (status, _) = await ObterAsync(ActumApiFactory.BancoInacessivel, "/health/live");

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task ReadyRetornaHealthyComBancoAcessivelAsync()
    {
        var (status, corpo) = await ObterAsync(postgres.ConnectionString, "/health/ready");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", corpo);
    }

    [Fact]
    public async Task ReadyRetornaUnhealthySemDetalhesComBancoInacessivelAsync()
    {
        var (status, corpo) = await ObterAsync(ActumApiFactory.BancoInacessivel, "/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Unhealthy", corpo);
    }

    private static async Task<(HttpStatusCode Status, string Corpo)> ObterAsync(string connectionString, string caminho)
    {
        await using var factory = new ActumApiFactory(Environments.Development, connectionString);
        using var cliente = factory.CreateClient();

        using var resposta = await cliente.GetAsync(caminho, TestContext.Current.CancellationToken);
        var corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (resposta.StatusCode, corpo);
    }
}
