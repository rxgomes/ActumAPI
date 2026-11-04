using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Tests;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Actum.Api.Tests;

/// <summary>
/// Cenários de <c>task-notes</c> e da validação de <c>rework-dashboard</c> pelo pipeline HTTP. O Asana simulado não
/// tem respostas roteirizadas: qualquer chamada a ele falharia o teste.
/// </summary>
[Collection(TarefasMigradasCollection.Nome)]
public sealed class AnotacaoApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";

    private readonly HttpMessageHandlerFalso _asana = new();

    private Guid _tarefa;

    private Guid _outraTarefa;

    public async ValueTask InitializeAsync()
    {
        await using var contexto = TarefasDbContext.Criar(postgres.DataSource);
        await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var sufixo = Guid.NewGuid().ToString("N");
        var empresa = new Empresa($"ws-{sufixo}", "Aureum-ERP", "#1976D2");
        contexto.Add(empresa);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        var tarefa = Tarefa.Criar(empresa, new AsanaTarefa($"t-{sufixo}", "Ajustar IPI", "", "", null, false, DateTime.Now, "", [], [], "user"), false, DateTime.Now);
        var outra = Tarefa.Criar(empresa, new AsanaTarefa($"o-{sufixo}", "Outra", "", "", null, false, DateTime.Now, "", [], [], "user"), false, DateTime.Now);
        outra.Arquivar(MotivoArquivamento.Excluida, DateTime.Now);
        contexto.AddRange(tarefa, outra);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tarefa = tarefa.Identifier;
        _outraTarefa = outra.Identifier;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CriarListarAlterarEExcluirSemChamarOAsanaAsync()
    {
        var primeira = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/anotacoes", new { texto = "  Testado com a NF 2958, ok  " });
        var segunda = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/anotacoes", new { texto = "Falta conferir o boleto" });

        Assert.Equal(HttpStatusCode.Created, primeira.Status);
        Assert.Equal("Testado com a NF 2958, ok", primeira.Corpo.GetProperty("texto").GetString());
        Assert.Equal(JsonValueKind.Null, primeira.Corpo.GetProperty("alteradaEm").ValueKind);

        var lista = await EnviarAsync(HttpMethod.Get, $"/tarefas/{_tarefa}/anotacoes", null);
        Assert.Equal(["Testado com a NF 2958, ok", "Falta conferir o boleto"], lista.Corpo.EnumerateArray().Select(item => item.GetProperty("texto").GetString()));

        var id = segunda.Corpo.GetProperty("identifier").GetGuid();
        var alterada = await EnviarAsync(HttpMethod.Put, $"/tarefas/{_tarefa}/anotacoes/{id}", new { texto = "Boleto conferido" });
        Assert.Equal(HttpStatusCode.OK, alterada.Status);
        Assert.Equal("Boleto conferido", alterada.Corpo.GetProperty("texto").GetString());
        Assert.NotEqual(JsonValueKind.Null, alterada.Corpo.GetProperty("alteradaEm").ValueKind);

        var excluida = await EnviarAsync(HttpMethod.Delete, $"/tarefas/{_tarefa}/anotacoes/{id}", null);
        Assert.Equal(HttpStatusCode.NoContent, excluida.Status);
        Assert.Single((await EnviarAsync(HttpMethod.Get, $"/tarefas/{_tarefa}/anotacoes", null)).Corpo.EnumerateArray());

        Assert.Empty(_asana.Requisicoes);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task TextoVazioRetorna400NoCampoTextoAsync(string? texto)
    {
        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/anotacoes", new { texto });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("Escreva a anotação.", corpo.GetProperty("errors").GetProperty("Texto")[0].GetString());
    }

    [Fact]
    public async Task TarefaArquivadaAceitaEAnotacaoDeOutraTarefaE404Async()
    {
        var naArquivada = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_outraTarefa}/anotacoes", new { texto = "Arquivada, mas anotada" });
        Assert.Equal(HttpStatusCode.Created, naArquivada.Status);

        var id = naArquivada.Corpo.GetProperty("identifier").GetGuid();
        var alterar = await EnviarAsync(HttpMethod.Put, $"/tarefas/{_tarefa}/anotacoes/{id}", new { texto = "x" });
        var excluir = await EnviarAsync(HttpMethod.Delete, $"/tarefas/{_tarefa}/anotacoes/{id}", null);
        var tarefaInexistente = await EnviarAsync(HttpMethod.Get, $"/tarefas/{Guid.NewGuid()}/anotacoes", null);

        Assert.Equal(HttpStatusCode.NotFound, alterar.Status);
        Assert.Equal(HttpStatusCode.NotFound, excluir.Status);
        Assert.Equal(HttpStatusCode.NotFound, tarefaInexistente.Status);
    }

    [Theory]
    [InlineData("?de=2026-10-01", "Ate", "Informe o fim do período.")]
    [InlineData("?de=2026-10-02&ate=2026-10-01", "Ate", "O início do período deve ser anterior ou igual ao fim.")]
    [InlineData("?de=2024-01-01&ate=2026-10-01", "Ate", "O período deve ter no máximo 24 meses.")]
    public async Task PeriodoInvalidoDosIndicadoresRetorna400Async(string consulta, string campo, string mensagem)
    {
        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Get, $"/retrabalhos/indicadores{consulta}", null);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal(mensagem, corpo.GetProperty("errors").GetProperty(campo)[0].GetString());
    }

    [Fact]
    public async Task IndicadoresEmCamelCaseSemChamarOAsanaAsync()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, "/retrabalhos/indicadores?de=2031-01-01&ate=2031-02-28", null);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["2031-01", "2031-02"], corpo.GetProperty("evolucao").EnumerateArray().Select(mes => mes.GetProperty("mes").GetString()));
        Assert.Equal(0, corpo.GetProperty("total").GetProperty("retrabalhos").GetInt32());
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("total").GetProperty("taxa").ValueKind);
        Assert.Empty(_asana.Requisicoes);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, string? Tipo)> EnviarAsync(HttpMethod metodo, string caminho, object? corpo)
    {
        await using var factory = new ActumApiFactory(Environments.Production, postgres.ConnectionString, asana: _asana);
        using var cliente = factory.CreateClient();

        using var requisicao = new HttpRequestMessage(metodo, caminho) { Content = corpo is null ? null : JsonContent.Create(corpo) };
        using var resposta = await cliente.SendAsync(requisicao, TestContext.Current.CancellationToken);
        var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (resposta.StatusCode, texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone(), resposta.Content.Headers.ContentType?.MediaType);
    }
}
