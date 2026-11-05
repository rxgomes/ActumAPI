using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Actum.Api.Tests;

/// <summary>
/// Cenários de <c>task-lifecycle</c> que dependem do pipeline HTTP: validação dos DTOs, tradução de
/// <c>DomainException</c> e de conflito e formato JSON da resposta.
/// </summary>
[Collection(TarefasMigradasCollection.Nome)]
public sealed class TarefaApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";

    private Guid _tarefa;

    public async ValueTask InitializeAsync()
    {
        await using var contexto = TarefasDbContext.Criar(postgres.DataSource);
        await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var sufixo = Guid.NewGuid().ToString("N");
        var empresa = new Empresa($"ws-{sufixo}", "Aureum-ERP", "#1976D2");
        contexto.Add(empresa);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        var asana = new AsanaTarefa($"t-{sufixo}", "Tarefa", "", "", null, false, DateTime.Now, "", [], [], "user");
        var tarefa = Tarefa.Criar(empresa, asana, cargaInicial: false, DateTime.Now);
        contexto.Add(tarefa);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tarefa = tarefa.Identifier;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task InicioSemCamposRetornaErrosPorCampoAsync()
    {
        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        var erros = corpo.GetProperty("errors");
        Assert.Equal(["A branch base é obrigatória."], erros.GetProperty("BranchBase").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["A branch de implementação é obrigatória."], erros.GetProperty("BranchImplementacao").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["O prazo interno é obrigatório."], erros.GetProperty("PrazoInterno").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task InicioComBranchesIguaisRetorna400ComMensagemAsync()
    {
        var (status, corpo, tipo) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "main", prazoInterno = "2026-11-10" });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("A branch base e a branch de implementação devem ser diferentes.", corpo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InicioValidoRespondeStatusEmSnakeCaseAsync()
    {
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "feature/ipi-devolvido", prazoInterno = "2026-11-10" });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("em_andamento", corpo.GetProperty("status").GetString());
        Assert.Equal("2026-11-10", corpo.GetProperty("prazoInterno").GetString());
        Assert.Equal(
            [("base", "main", false), ("implementacao", "feature/ipi-devolvido", false)],
            corpo.GetProperty("branches").EnumerateArray().Select(b => (b.GetProperty("tipo").GetString(), b.GetProperty("nome").GetString(), b.GetProperty("enviada").GetBoolean())));

        var (statusDeNovo, corpoDeNovo, _) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "outra", prazoInterno = "2026-11-10" });
        Assert.Equal(HttpStatusCode.BadRequest, statusDeNovo);
        Assert.Contains("Status atual: em andamento", corpoDeNovo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task PrioridadeInvalidaRetorna400Async()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Patch, $"/tarefas/{_tarefa}", new { prioridade = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(["A prioridade deve estar entre 1 e 3."], corpo.GetProperty("errors").GetProperty("Prioridade").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task BranchRepetidaRetorna409Async()
    {
        await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "feature/ipi-devolvido", prazoInterno = "2026-11-10" });

        var (criada, branch, _) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/branches", new { tipo = "implementacao", nome = "fix/pagamento" });
        Assert.Equal(HttpStatusCode.Created, criada);
        Assert.Equal("implementacao", branch.GetProperty("tipo").GetString());

        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/branches", new { tipo = "implementacao", nome = "fix/pagamento" });
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("A tarefa já tem uma branch com esse nome.", corpo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TarefaInexistenteRetorna404Async()
    {
        var (status, _, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{Guid.CreateVersion7()}/bloquear", null);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal(ProblemJson, tipo);
    }

    [Fact]
    public async Task ListagemComTamanhoAcimaDoMaximoRetorna400Async()
    {
        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Get, "/tarefas?tamanho=500", null);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal(["O tamanho da página deve estar entre 1 e 100."], corpo.GetProperty("errors").GetProperty("Tamanho").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task PainelComStatusArquivadaRetorna400Async()
    {
        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Get, "/tarefas/painel?status=arquivada", null);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal(
            ["O status deve ser entrada, em_andamento, bloqueada, retrabalho ou concluida."],
            corpo.GetProperty("errors").GetProperty("Status").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task DetalheEPainelRespondemEmJsonEInexistenteRetorna404Async()
    {
        var (status, detalhe, _) = await EnviarAsync(HttpMethod.Get, $"/tarefas/{_tarefa}", null);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("entrada", detalhe.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Array, detalhe.GetProperty("retrabalhos").ValueKind);

        var (statusPainel, painel, _) = await EnviarAsync(HttpMethod.Get, "/tarefas/painel?busca=Tarefa", null);
        Assert.Equal(HttpStatusCode.OK, statusPainel);
        Assert.Equal(8, painel.GetProperty("grupos").GetArrayLength());

        var (statusInexistente, _, tipo) = await EnviarAsync(HttpMethod.Get, $"/tarefas/{Guid.CreateVersion7()}", null);
        Assert.Equal(HttpStatusCode.NotFound, statusInexistente);
        Assert.Equal(ProblemJson, tipo);
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, string? Tipo)> EnviarAsync(HttpMethod metodo, string caminho, object? corpo)
    {
        await using var factory = new ActumApiFactory(Environments.Production, postgres.ConnectionString);
        using var cliente = factory.CreateClient();

        using var requisicao = new HttpRequestMessage(metodo, caminho) { Content = corpo is null ? null : JsonContent.Create(corpo) };
        using var resposta = await cliente.SendAsync(requisicao, TestContext.Current.CancellationToken);
        var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone(), resposta.Content.Headers.ContentType?.MediaType);
    }
}
