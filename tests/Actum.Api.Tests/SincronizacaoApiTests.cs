using System.Net;
using System.Text.Json;
using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Persistence;
using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Actum.Api.Tests;

/// <summary>
/// Cenário de <c>task-sync</c> que depende do pipeline HTTP: sincronização simultânea responde <c>409</c>.
/// </summary>
[Collection(TarefasMigradasCollection.Nome)]
public sealed class SincronizacaoApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await using var contexto = TarefasDbContext.Criar(postgres.DataSource);
        await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SincronizacaoSimultaneaRetorna409EmProblemDetailsAsync()
    {
        await using var factory = new ActumApiFactory(Environments.Production, postgres.ConnectionString);
        using var cliente = factory.CreateClient();

        var emAndamento = factory.Services.GetRequiredService<SincronizacaoEmAndamento>();
        Assert.True(emAndamento.TentarIniciar());
        try
        {
            using var resposta = await cliente.PostAsync("/sincronizacoes", null, TestContext.Current.CancellationToken);
            using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
            Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
            Assert.Equal("Conflito.", corpo.RootElement.GetProperty("title").GetString());
            Assert.Contains("sincronização em andamento", corpo.RootElement.GetProperty("detail").GetString());
        }
        finally
        {
            emAndamento.Finalizar();
        }
    }
}
