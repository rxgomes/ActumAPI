using System.Net;
using System.Text.Json;
using Actum.Modules.Asana.Application;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Actum.Modules.Asana.Tests;

/// <summary>
/// Cenários da capability <c>asana-client</c> no cliente, sem rede.
/// </summary>
public sealed class AsanaClientTests
{
    private const string Token = "token-de-teste";

    private const string Workspace = "111";

    private const string NomeDaTag = "feature/ipi-devolvido";

    private readonly HttpMessageHandlerFalso _asana = new();

    [Fact]
    public async Task RequisicaoLevaTokenComoBearerAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new { data = Usuario() });

        var usuario = await Criar().ObterUsuarioAtualAsync(TestContext.Current.CancellationToken);

        var requisicao = Assert.Single(_asana.Requisicoes);
        Assert.Equal("Bearer", requisicao.Autorizacao?.Scheme);
        Assert.Equal(Token, requisicao.Autorizacao?.Parameter);
        Assert.Equal("https://app.asana.com/api/1.0/users/me?opt_fields=name,workspaces.name", requisicao.Uri.ToString());
        Assert.Equal("Ricardo", usuario.Nome);
        Assert.Equal([new AsanaWorkspace("111", "Aureum-ERP"), new AsanaWorkspace("222", "aremas.com.br")], usuario.Workspaces);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TokenAusenteFalhaSemChamarOAsanaAsync(string? token)
    {
        var erro = await Assert.ThrowsAsync<AsanaNaoConfiguradoException>(
            () => Criar(token).ObterUsuarioAtualAsync(TestContext.Current.CancellationToken));

        Assert.Contains("não está configurado", erro.Message);
        Assert.Empty(_asana.Requisicoes);
    }

    [Fact]
    public async Task ListagemPercorreTodasAsPaginasAsync()
    {
        _asana
            .Responder(HttpStatusCode.OK, new { data = Tarefas(1, 100), next_page = new { offset = "pagina2", path = "/tasks", uri = "https://app.asana.com" } })
            .Responder(HttpStatusCode.OK, new { data = Tarefas(101, 20), next_page = (object?)null });

        var tarefas = await Criar().ListarTarefasAbertasAsync(Workspace, TestContext.Current.CancellationToken);

        Assert.Equal(120, tarefas.Count);
        Assert.Equal(Enumerable.Range(1, 120).Select(gid => gid.ToString()), tarefas.Select(tarefa => tarefa.Gid));

        var primeira = _asana.Requisicoes[0].Uri.Query;
        Assert.Contains("assignee=me", primeira);
        Assert.Contains($"workspace={Workspace}", primeira);
        Assert.Contains("completed_since=now", primeira);
        Assert.DoesNotContain("offset=", primeira);
        Assert.Contains("offset=pagina2", _asana.Requisicoes[1].Uri.Query);
    }

    [Fact]
    public async Task TarefaTrazTodosOsCamposAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new { data = Tarefa("42", dueOn: "2026-11-20", modifiedAt: "2026-11-03T14:30:15.123Z") });

        var tarefa = await Criar().ObterTarefaAsync("42", TestContext.Current.CancellationToken);

        Assert.NotNull(tarefa);
        Assert.Equal("42", tarefa.Gid);
        Assert.Equal("Tarefa 42", tarefa.Nome);
        Assert.Equal("notas 42", tarefa.Notas);
        Assert.Equal("<body>notas 42</body>", tarefa.NotasHtml);
        Assert.Equal(new DateOnly(2026, 11, 20), tarefa.PrazoEm);
        Assert.False(tarefa.Concluida);
        Assert.Equal(ParaLocal("2026-11-03T14:30:15.123Z"), tarefa.ModificadaEm);
        Assert.Equal(DateTimeKind.Unspecified, tarefa.ModificadaEm.Kind);
        Assert.Equal("https://app.asana.com/0/0/42", tarefa.Link);
        Assert.Equal(["Projeto A", "Projeto B"], tarefa.Projetos);
        Assert.Equal([new AsanaTag("9", "main")], tarefa.Tags);
        Assert.Equal("7", tarefa.ResponsavelGid);
    }

    [Fact]
    public async Task TarefaSemPrazoTemPrazoNuloAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new { data = Tarefa("42", dueOn: null) });

        var tarefa = await Criar().ObterTarefaAsync("42", TestContext.Current.CancellationToken);

        Assert.Null(tarefa!.PrazoEm);
    }

    [Fact]
    public async Task TarefaInexistenteDevolveNuloAsync()
    {
        _asana.Responder(HttpStatusCode.NotFound, new { errors = new[] { new { message = "Not found" } } });

        var tarefa = await Criar().ObterTarefaAsync("42", TestContext.Current.CancellationToken);

        Assert.Null(tarefa);
    }

    [Fact]
    public async Task StoriesSaoClassificadasComDataLocalEAutorAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new
        {
            data = new object[]
            {
                new { gid = "1", resource_subtype = "marked_incomplete", created_at = "2026-11-02T10:00:00.000Z", created_by = new { gid = "7", name = "Fulano" }, text = "Fulano marcou como incompleta" },
                new { gid = "2", resource_subtype = "comment_added", created_at = "2026-11-02T11:00:00.000Z", created_by = new { gid = "8", name = "Beltrano" }, text = "Faltou o IPI" },
                new { gid = "3", resource_subtype = "marked_complete", created_at = "2026-11-02T12:00:00.000Z", created_by = new { gid = "7", name = "Fulano" }, text = "concluída" },
                new { gid = "4", resource_subtype = "due_date_changed", created_at = "2026-11-02T13:00:00.000Z", created_by = (object?)null, text = "prazo alterado" },
            },
        });

        var stories = await Criar().ListarStoriesAsync("42", TestContext.Current.CancellationToken);

        Assert.Equal(
            [AsanaStoryTipo.MarcadaIncompleta, AsanaStoryTipo.Comentario, AsanaStoryTipo.MarcadaConcluida, AsanaStoryTipo.Outro],
            stories.Select(story => story.Tipo));

        var reabertura = stories[0];
        Assert.Equal(ParaLocal("2026-11-02T10:00:00.000Z"), reabertura.CriadaEm);
        Assert.Equal(DateTimeKind.Unspecified, reabertura.CriadaEm.Kind);
        Assert.Equal("Fulano", reabertura.AutorNome);

        Assert.Equal("Faltou o IPI", stories[1].Texto);
        Assert.Null(stories[3].AutorNome);
        Assert.StartsWith("/api/1.0/tasks/42/stories", _asana.Requisicoes[0].Uri.AbsolutePath);
    }

    [Fact]
    public async Task TagExistenteEDevolvidaSemCriarOutraAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new { data = new[] { new { gid = "1", name = "main" }, new { gid = "2", name = NomeDaTag } } });

        var tag = await Criar().ObterOuCriarTagAsync(Workspace, NomeDaTag, TestContext.Current.CancellationToken);

        Assert.Equal(new AsanaTag("2", NomeDaTag), tag);
        var requisicao = Assert.Single(_asana.Requisicoes);
        Assert.Equal(HttpMethod.Get, requisicao.Metodo);
        Assert.Equal($"/api/1.0/workspaces/{Workspace}/tags", requisicao.Uri.AbsolutePath);
    }

    [Fact]
    public async Task TagInexistenteECriadaNoWorkspaceAsync()
    {
        _asana
            .Responder(HttpStatusCode.OK, new { data = new[] { new { gid = "1", name = "main" } } })
            .Responder(HttpStatusCode.Created, new { data = new { gid = "3", name = NomeDaTag } });

        var tag = await Criar().ObterOuCriarTagAsync(Workspace, NomeDaTag, TestContext.Current.CancellationToken);

        Assert.Equal(new AsanaTag("3", NomeDaTag), tag);
        var criacao = _asana.Requisicoes[1];
        Assert.Equal(HttpMethod.Post, criacao.Metodo);
        Assert.Equal($"/api/1.0/workspaces/{Workspace}/tags", criacao.Uri.AbsolutePath);
        Assert.Equal(NomeDaTag, Corpo(criacao).GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task TagComMaiusculasDiferentesECriadaAsync()
    {
        _asana
            .Responder(HttpStatusCode.OK, new { data = new[] { new { gid = "1", name = "Main" } } })
            .Responder(HttpStatusCode.Created, new { data = new { gid = "2", name = "main" } });

        var tag = await Criar().ObterOuCriarTagAsync(Workspace, "main", TestContext.Current.CancellationToken);

        Assert.Equal(new AsanaTag("2", "main"), tag);
        Assert.Equal(HttpMethod.Post, _asana.Requisicoes[1].Metodo);
        Assert.Equal("main", Corpo(_asana.Requisicoes[1]).GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task AdicionarTagEnviaATagParaATarefaAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new { data = new { } });

        await Criar().AdicionarTagAsync("42", "9", TestContext.Current.CancellationToken);

        var requisicao = Assert.Single(_asana.Requisicoes);
        Assert.Equal(HttpMethod.Post, requisicao.Metodo);
        Assert.Equal("/api/1.0/tasks/42/addTag", requisicao.Uri.AbsolutePath);
        Assert.Equal("9", Corpo(requisicao).GetProperty("data").GetProperty("tag").GetString());
    }

    [Fact]
    public async Task ConcluirEnviaPutComCompletedAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new { data = new { gid = "42", completed = true } });

        await Criar().ConcluirTarefaAsync("42", TestContext.Current.CancellationToken);

        var requisicao = Assert.Single(_asana.Requisicoes);
        Assert.Equal(HttpMethod.Put, requisicao.Metodo);
        Assert.Equal("/api/1.0/tasks/42", requisicao.Uri.AbsolutePath);
        Assert.True(Corpo(requisicao).GetProperty("data").GetProperty("completed").GetBoolean());
    }

    [Fact]
    public async Task LimiteTemporarioERepetidoAposRetryAfterAsync()
    {
        _asana
            .Responder(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(1))
            .Responder(HttpStatusCode.OK, new { data = Usuario() });
        var inicio = TimeProvider.System.GetTimestamp();

        var usuario = await Criar().ObterUsuarioAtualAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Ricardo", usuario.Nome);
        Assert.Equal(2, _asana.Requisicoes.Count);
        Assert.True(TimeProvider.System.GetElapsedTime(inicio) >= TimeSpan.FromMilliseconds(900));
    }

    [Fact]
    public async Task LimitePersistenteFalhaComoIndisponivelApos3NovasTentativasAsync()
    {
        for (var i = 0; i < 4; i++)
        {
            _asana.Responder(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.Zero);
        }

        await Assert.ThrowsAsync<AsanaIndisponivelException>(
            () => Criar().ObterUsuarioAtualAsync(TestContext.Current.CancellationToken));

        Assert.Equal(4, _asana.Requisicoes.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task TokenRecusadoFalhaComoAcessoNegadoAsync(HttpStatusCode status)
    {
        _asana.Responder(status, new { errors = new[] { new { message = "Not Authorized" } } });

        await Assert.ThrowsAsync<AsanaAcessoNegadoException>(
            () => Criar().ObterUsuarioAtualAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ErroDoServidorFalhaComoIndisponivelAsync()
    {
        _asana.Responder(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<AsanaIndisponivelException>(
            () => Criar().ListarTarefasAbertasAsync(Workspace, TestContext.Current.CancellationToken));
    }

    private AsanaClient Criar(string? token = Token)
    {
        var httpClient = new HttpClient(_asana);
        AsanaClient.Configurar(httpClient);

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AsanaClient.TokenKey] = token })
            .Build();

        return new AsanaClient(httpClient, configuracao, NullLogger<AsanaClient>.Instance);
    }

    private static object Usuario()
    {
        return new
        {
            gid = "7",
            name = "Ricardo",
            workspaces = new[] { new { gid = "111", name = "Aureum-ERP" }, new { gid = "222", name = "aremas.com.br" } },
        };
    }

    private static object[] Tarefas(int primeiroGid, int quantidade)
    {
        return Enumerable.Range(primeiroGid, quantidade).Select(gid => Tarefa(gid.ToString())).ToArray();
    }

    private static object Tarefa(string gid, string? dueOn = "2026-11-20", string modifiedAt = "2026-11-03T14:30:15.123Z")
    {
        return new
        {
            gid,
            name = $"Tarefa {gid}",
            notes = $"notas {gid}",
            html_notes = $"<body>notas {gid}</body>",
            due_on = dueOn,
            completed = false,
            modified_at = modifiedAt,
            permalink_url = $"https://app.asana.com/0/0/{gid}",
            projects = new[] { new { gid = "50", name = "Projeto A" }, new { gid = "51", name = "Projeto B" } },
            tags = new[] { new { gid = "9", name = "main" } },
            assignee = new { gid = "7" },
        };
    }

    private static DateTime ParaLocal(string utc)
    {
        return DateTime.SpecifyKind(DateTimeOffset.Parse(utc).ToLocalTime().DateTime, DateTimeKind.Unspecified);
    }

    private static JsonElement Corpo(RequisicaoRegistrada requisicao)
    {
        return JsonDocument.Parse(requisicao.Corpo!).RootElement;
    }
}
