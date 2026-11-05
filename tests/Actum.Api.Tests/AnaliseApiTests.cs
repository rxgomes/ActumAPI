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
/// Cenários de <c>claude-client</c> e <c>task-analysis</c> que dependem do pipeline HTTP: <c>GET /claude/situacao</c>,
/// tradução das falhas da Claude API em Problem Details, validação e formato JSON das análises. A Anthropic é
/// simulada pelo <see cref="HttpMessageHandlerFalso"/>: nenhuma chamada real (nem cobrada) é feita.
/// </summary>
[Collection(TarefasMigradasCollection.Nome)]
public sealed class AnaliseApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";

    private const string Chave = "chave-secreta-de-teste";

    private readonly HttpMessageHandlerFalso _anthropic = new();

    private Guid _tarefa;

    public async ValueTask InitializeAsync()
    {
        await using var contexto = TarefasDbContext.Criar(postgres.DataSource);
        await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var sufixo = Guid.NewGuid().ToString("N");
        var empresa = new Empresa($"ws-{sufixo}", "Aureum-ERP", "#1976D2");
        contexto.Add(empresa);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        var asana = new AsanaTarefa($"t-{sufixo}", "Ajustar IPI", "Abater o IPI devolvido.", "", null, false, DateTime.Now, "", ["Fiscal"], [], "user");
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
    public async Task SituacaoComChaveNaoDevolveAChaveAsync()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, "/claude/situacao", null);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(corpo.GetProperty("configurado").GetBoolean());
        Assert.Equal("claude-opus-5-5", corpo.GetProperty("modelo").GetString());
        Assert.DoesNotContain(Chave, corpo.GetRawText());
        Assert.Empty(_anthropic.Requisicoes);
    }

    [Fact]
    public async Task SituacaoSemChaveAsync()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, "/claude/situacao", null, chave: string.Empty);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(corpo.GetProperty("configurado").GetBoolean());
    }

    [Fact]
    public async Task AnaliseCriadaRetorna201ComOsItensEmCamelCaseAsync()
    {
        ResponderHistorico();
        ResponderEstruturado("Abater o IPI devolvido.", ["Ajustar cálculo"], ["Vale para notas antigas?"]);

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/analises", null);

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal(1, corpo.GetProperty("versao").GetInt32());
        Assert.True(corpo.GetProperty("atual").GetBoolean());
        Assert.Equal("Abater o IPI devolvido.", corpo.GetProperty("objetivo").GetString());
        var grupo = Assert.Single(corpo.GetProperty("checklist").EnumerateArray());
        Assert.Equal("Back", grupo.GetProperty("nome").GetString());
        var item = Assert.Single(grupo.GetProperty("itens").EnumerateArray());
        Assert.Equal("Back", item.GetProperty("grupo").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("vago").ValueKind);
        var teste = Assert.Single(corpo.GetProperty("testes").EnumerateArray());
        Assert.Equal(("teste", "Emitir a NF 2958"), (teste.GetProperty("tipo").GetString(), teste.GetProperty("texto").GetString()));
        Assert.Equal("checklist", item.GetProperty("tipo").GetString());
        Assert.False(item.GetProperty("concluido").GetBoolean());
        Assert.Equal("duvida", Assert.Single(corpo.GetProperty("duvidas").EnumerateArray()).GetProperty("tipo").GetString());
        Assert.Empty(corpo.GetProperty("regras").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("origem").ValueKind);
        Assert.Equal(1200, corpo.GetProperty("tokensEntrada").GetInt32());

        var lista = await EnviarAsync(HttpMethod.Get, $"/tarefas/{_tarefa}/analises", null);
        Assert.Equal(HttpStatusCode.OK, lista.Status);
        Assert.Single(lista.Corpo.EnumerateArray());
    }

    [Fact]
    public async Task ChaveAusenteRetorna400SemChamarAAnthropicAsync()
    {
        // Os comentários são atualizados pelo Asana antes da IA; a Anthropic não é chamada.
        ResponderHistorico();
        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/analises", null, chave: string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Contains("dotnet user-secrets set \"Claude:ApiKey\"", corpo.GetProperty("detail").GetString());
        Assert.DoesNotContain(_anthropic.Requisicoes, requisicao => requisicao.Uri.Host == "api.anthropic.com");
    }

    [Fact]
    public async Task ChaveRecusadaRetorna400Async()
    {
        ResponderHistorico();
        _anthropic.Responder(HttpStatusCode.Unauthorized, new { type = "error", error = new { type = "authentication_error", message = "invalid x-api-key" } });

        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/analises", null);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("A chave da Claude API é inválida ou não tem permissão para usar o modelo configurado.", corpo.GetProperty("detail").GetString());
        Assert.DoesNotContain(Chave, corpo.GetRawText());
    }

    [Fact]
    public async Task ContaSemCreditoRetorna400ComOrientacaoAsync()
    {
        ResponderHistorico();
        _anthropic.Responder(HttpStatusCode.BadRequest, new
        {
            type = "error",
            error = new { type = "invalid_request_error", message = "Your credit balance is too low to access the Anthropic API." },
        });

        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/analises", null);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal(
            "A conta da Anthropic está sem créditos para usar a Claude API. Adicione créditos em Plans & Billing, no Console da Anthropic, e tente novamente.",
            corpo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task RecusaDoModeloRetorna400Async()
    {
        ResponderHistorico();
        ResponderMensagem(string.Empty, "refusal");

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/analises", null);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.StartsWith("A IA não analisou esta demanda", corpo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task SobrecargaRetorna502SemGravarAnaliseAsync()
    {
        ResponderHistorico();
        _anthropic.Responder((HttpStatusCode)529, new { type = "error", error = new { type = "overloaded_error", message = "Overloaded" } });

        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/analises", null);

        Assert.Equal(HttpStatusCode.BadGateway, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("Não foi possível comunicar com a Claude API. Tente novamente em instantes.", corpo.GetProperty("detail").GetString());
        Assert.Empty((await EnviarAsync(HttpMethod.Get, $"/tarefas/{_tarefa}/analises", null)).Corpo.EnumerateArray());
    }

    [Fact]
    public async Task RespostaTruncadaRetorna502Async()
    {
        ResponderHistorico();
        ResponderMensagem("{\"objetivo\": \"Aba", "max_tokens");

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/analises", null);

        Assert.Equal(HttpStatusCode.BadGateway, status);
        Assert.Equal("A IA devolveu uma resposta incompleta. Tente analisar novamente.", corpo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TarefaInexistenteRetorna404Async()
    {
        var analise = await EnviarAsync(HttpMethod.Post, $"/tarefas/{Guid.NewGuid()}/analises", null);
        var lista = await EnviarAsync(HttpMethod.Get, $"/tarefas/{Guid.NewGuid()}/analises", null);
        var item = await EnviarAsync(HttpMethod.Patch, $"/tarefas/{_tarefa}/analises/itens/{Guid.NewGuid()}", new { concluido = true });

        Assert.Equal(HttpStatusCode.NotFound, analise.Status);
        Assert.Equal("Tarefa não encontrada.", analise.Corpo.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.NotFound, lista.Status);
        Assert.Equal(HttpStatusCode.NotFound, item.Status);
        Assert.Empty(_anthropic.Requisicoes);
    }

    [Fact]
    public async Task MarcacaoSemConcluidoRetornaErroPorCampoAsync()
    {
        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Patch, $"/tarefas/{_tarefa}/analises/itens/{Guid.NewGuid()}", new { });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("Informe se o item está concluído.", corpo.GetProperty("errors").GetProperty("Concluido")[0].GetString());
    }

    [Fact]
    public async Task MarcacaoDeItemDaAnaliseAtualAsync()
    {
        ResponderHistorico();
        ResponderEstruturado("Objetivo", ["Item"], []);
        var analise = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/analises", null);
        var item = analise.Corpo.GetProperty("checklist")[0].GetProperty("itens")[0].GetProperty("identifier").GetGuid();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Patch, $"/tarefas/{_tarefa}/analises/itens/{item}", new { concluido = true });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(corpo.GetProperty("concluido").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, corpo.GetProperty("concluidoEm").ValueKind);
    }

    private void ResponderEstruturado(string objetivo, string[] checklist, string[] duvidas)
    {
        ResponderMensagem(
            JsonSerializer.Serialize(new
            {
                objetivo,
                checklist = checklist.Select(texto => new { grupo = "Back", texto, vago = "" }),
                testes = new[] { "Emitir a NF 2958" },
                duvidas,
            }),
            "end_turn");
    }

    /// <summary>
    /// Enfileira a resposta do Asana ao histórico da tarefa, lido pela análise antes de chamar a IA (o handler
    /// atende Asana e Anthropic em fila)
    /// </summary>
    private void ResponderHistorico(params object[] stories)
    {
        _anthropic.Responder(HttpStatusCode.OK, new { data = stories, next_page = (object?)null });
    }

    private void ResponderMensagem(string texto, string stopReason)
    {
        _anthropic.Responder(HttpStatusCode.OK, new
        {
            id = "msg_teste",
            type = "message",
            role = "assistant",
            model = "claude-opus-5-5",
            content = new object[] { new { type = "text", text = texto } },
            stop_reason = stopReason,
            stop_sequence = (string?)null,
            usage = new { input_tokens = 1200, output_tokens = 345 },
        });
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, string? Tipo)> EnviarAsync(
        HttpMethod metodo,
        string caminho,
        object? corpo,
        string chave = Chave)
    {
        // Production: não carrega os user-secrets da máquina, então só vale a chave informada aqui.
        var settings = new Dictionary<string, string>
        {
            ["Claude:ApiKey"] = chave,
            ["Claude:NovasTentativas"] = "0",
            ["Asana:Token"] = "token-de-teste",
        };
        await using var factory = new ActumApiFactory(Environments.Production, postgres.ConnectionString, settings, asana: _anthropic);
        using var cliente = factory.CreateClient();

        using var requisicao = new HttpRequestMessage(metodo, caminho) { Content = corpo is null ? null : JsonContent.Create(corpo) };
        using var resposta = await cliente.SendAsync(requisicao, TestContext.Current.CancellationToken);
        var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone(), resposta.Content.Headers.ContentType?.MediaType);
    }
}
