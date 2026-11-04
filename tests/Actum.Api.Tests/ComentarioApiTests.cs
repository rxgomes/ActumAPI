using System.Net;
using System.Net.Http.Headers;
using System.Text;
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
/// Cenários de <c>task-comments</c> que dependem do pipeline HTTP: formulário multipart, validação em pt-BR,
/// formato JSON e Problem Details com <c>anexosEnviados</c>. O Asana é simulado pelo <see cref="HttpMessageHandlerFalso"/>.
/// </summary>
[Collection(TarefasMigradasCollection.Nome)]
public sealed class ComentarioApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";

    private readonly HttpMessageHandlerFalso _asana = new();

    private Guid _tarefa;

    private string _tarefaGid = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await using var contexto = TarefasDbContext.Criar(postgres.DataSource);
        await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var sufixo = Guid.NewGuid().ToString("N");
        var empresa = new Empresa($"ws-{sufixo}", "Aureum-ERP", "#1976D2");
        contexto.Add(empresa);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tarefaGid = $"t-{sufixo}";
        var asana = new AsanaTarefa(_tarefaGid, "Ajustar IPI", "", "", null, false, DateTime.Now, "", [], [], "user");
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
    public async Task ParticipantesEmCamelCaseAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new { data = new { gid = _tarefaGid, followers = new[] { new { gid = "9", name = "Maria" } } } })
            .Responder(HttpStatusCode.OK, new { data = new { gid = "7", name = "Ricardo", workspaces = Array.Empty<object>() } });

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Get, $"/tarefas/{_tarefa}/participantes", null);

        Assert.Equal(HttpStatusCode.OK, status);
        var participante = Assert.Single(corpo.EnumerateArray());
        Assert.Equal(("9", "Maria", false), (participante.GetProperty("gid").GetString(), participante.GetProperty("nome").GetString(), participante.GetProperty("sugerido").GetBoolean()));
    }

    [Fact]
    public async Task ComentarioComMencaoEPrintRetorna201Async()
    {
        _asana.Responder(HttpStatusCode.OK, new { data = new { gid = _tarefaGid, followers = new[] { new { gid = "9", name = "Maria" } } } })
            .Responder(HttpStatusCode.OK, new { data = new { gid = "a1", name = "print.png" } })
            .Responder(HttpStatusCode.OK, new
            {
                data = new { gid = $"s-{Guid.NewGuid():N}", resource_subtype = "comment_added", created_at = "2026-11-03T21:15:00Z", created_by = new { gid = "7", name = "Ricardo" }, text = "Maria Segue o erro\n\nAnexos: print.png" },
            });

        using var formulario = Formulario("Segue o erro", ["9"], ("print.png", "image/png", "conteudo-png"));
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/comentarios", formulario);

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal(["print.png"], corpo.GetProperty("anexos").EnumerateArray().Select(anexo => anexo.GetString()));
        Assert.True(corpo.GetProperty("comentario").GetProperty("lido").GetBoolean());
        Assert.Equal("Ricardo", corpo.GetProperty("comentario").GetProperty("autor").GetString());

        var requisicoes = _asana.Requisicoes;
        Assert.Equal(3, requisicoes.Count);
        Assert.Contains("conteudo-png", requisicoes[1].Corpo);
        using var comentario = JsonDocument.Parse(requisicoes[2].Corpo!);
        Assert.Equal(
            "<body><a data-asana-gid=\"9\"/> Segue o erro\n\nAnexos: print.png</body>",
            comentario.RootElement.GetProperty("data").GetProperty("html_text").GetString());
    }

    [Fact]
    public async Task SemConteudoRetorna400EmPortuguesAsync()
    {
        using var formulario = Formulario("  ", []);

        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/comentarios", formulario);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("Escreva um comentário ou anexe um arquivo.", corpo.GetProperty("detail").GetString());
        Assert.Empty(_asana.Requisicoes);
    }

    [Fact]
    public async Task ArquivoGrandeDemaisRetorna400ComONomeAsync()
    {
        using var formulario = Formulario(null, [], ("grande.bin", "application/octet-stream", new string('x', (25 * 1024 * 1024) + 1)));

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/comentarios", formulario);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("O arquivo grande.bin tem mais de 25 MB, o limite por arquivo.", corpo.GetProperty("detail").GetString());
        Assert.Empty(_asana.Requisicoes);
    }

    [Fact]
    public async Task TarefaInexistenteRetorna404Async()
    {
        using var formulario = Formulario("oi", []);

        var envio = await EnviarAsync(HttpMethod.Post, $"/tarefas/{Guid.NewGuid()}/comentarios", formulario);
        var participantes = await EnviarAsync(HttpMethod.Get, $"/tarefas/{Guid.NewGuid()}/participantes", null);

        Assert.Equal(HttpStatusCode.NotFound, envio.Status);
        Assert.Equal(HttpStatusCode.NotFound, participantes.Status);
        Assert.Empty(_asana.Requisicoes);
    }

    [Fact]
    public async Task FalhaDepoisDoAnexoRetorna502ComAnexosEnviadosAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new { data = new { gid = "a1", name = "print.png" } })
            .Responder(HttpStatusCode.ServiceUnavailable);

        using var formulario = Formulario("Segue", [], ("print.png", "image/png", "png"));
        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/comentarios", formulario);

        Assert.Equal(HttpStatusCode.BadGateway, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal(["print.png"], corpo.GetProperty("anexosEnviados").EnumerateArray().Select(anexo => anexo.GetString()));
        Assert.Contains("print.png já foram enviados ao Asana", corpo.GetProperty("detail").GetString());
    }

    private static MultipartFormDataContent Formulario(string? texto, string[] mencoes, params (string Nome, string Tipo, string Conteudo)[] arquivos)
    {
        var formulario = new MultipartFormDataContent();
        if (texto is not null)
        {
            formulario.Add(new StringContent(texto), "texto");
        }

        foreach (var mencao in mencoes)
        {
            formulario.Add(new StringContent(mencao), "mencoes");
        }

        foreach (var (nome, tipo, conteudo) in arquivos)
        {
            var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes(conteudo));
            arquivo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
            formulario.Add(arquivo, "arquivos", nome);
        }

        return formulario;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, string? Tipo)> EnviarAsync(HttpMethod metodo, string caminho, HttpContent? conteudo)
    {
        // Production: não carrega os user-secrets da máquina, então só vale o token informado aqui.
        var settings = new Dictionary<string, string> { ["Asana:Token"] = "token-de-teste" };
        await using var factory = new ActumApiFactory(Environments.Production, postgres.ConnectionString, settings, asana: _asana);
        using var cliente = factory.CreateClient();

        using var requisicao = new HttpRequestMessage(metodo, caminho) { Content = conteudo };
        using var resposta = await cliente.SendAsync(requisicao, TestContext.Current.CancellationToken);
        var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone(), resposta.Content.Headers.ContentType?.MediaType);
    }
}
