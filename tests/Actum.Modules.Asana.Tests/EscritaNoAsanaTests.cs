using System.Net;
using System.Text;
using System.Text.Json;
using Actum.Modules.Asana.Application;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Asana.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Actum.Modules.Asana.Tests;

/// <summary>
/// Cenários de participantes, anexo e comentário da capability <c>asana-client</c>, sem rede.
/// </summary>
public sealed class EscritaNoAsanaTests
{
    private const string Token = "token-secreto-de-teste";

    private const string Tarefa = "1210000000000001";

    private readonly HttpMessageHandlerFalso _asana = new();

    private readonly LoggerDeTeste _logger = new();

    [Fact]
    public async Task ParticipantesSaoOsSeguidoresDaTarefaAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new
        {
            data = new { gid = Tarefa, followers = new[] { new { gid = "7", name = "Ricardo" }, new { gid = "9", name = "Maria Angelica" } } },
        });

        var participantes = await Criar().ListarParticipantesAsync(Tarefa, TestContext.Current.CancellationToken);

        Assert.Equal([new AsanaUsuarioResumo("7", "Ricardo"), new AsanaUsuarioResumo("9", "Maria Angelica")], participantes);
        Assert.Equal($"https://app.asana.com/api/1.0/tasks/{Tarefa}?opt_fields=followers.name", _asana.Requisicoes.Single().Uri.ToString());
    }

    [Fact]
    public async Task ParticipantesDeTarefaExcluidaSaoNulosAsync()
    {
        _asana.Responder(HttpStatusCode.NotFound, new { errors = new[] { new { message = "Not found" } } });

        Assert.Null(await Criar().ListarParticipantesAsync(Tarefa, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnexoVaiComoMultipartComTarefaNomeETipoAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new { data = new { gid = "a1", name = "print-20261103-181500.png" } });
        using var conteudo = new MemoryStream(Encoding.UTF8.GetBytes("bytes-do-print"));

        var anexo = await Criar().AnexarArquivoAsync(Tarefa, "print-20261103-181500.png", "image/png", conteudo, TestContext.Current.CancellationToken);

        Assert.Equal(new AsanaAnexo("a1", "print-20261103-181500.png"), anexo);
        var requisicao = Assert.Single(_asana.Requisicoes);
        Assert.Equal(HttpMethod.Post, requisicao.Metodo);
        Assert.Equal("https://app.asana.com/api/1.0/attachments?opt_fields=name", requisicao.Uri.ToString());
        Assert.Contains("name=parent", requisicao.Corpo);
        Assert.Contains(Tarefa, requisicao.Corpo);
        Assert.Contains("name=file; filename=print-20261103-181500.png", requisicao.Corpo);
        Assert.Contains("Content-Type: image/png", requisicao.Corpo);
        Assert.Contains("bytes-do-print", requisicao.Corpo);
    }

    [Fact]
    public async Task AnexoRepeteEm429ComOMesmoConteudoAsync()
    {
        _asana.Responder(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.Zero)
            .Responder(HttpStatusCode.OK, new { data = new { gid = "a1", name = "log.txt" } });
        using var conteudo = new MemoryStream(Encoding.UTF8.GetBytes("linha do log"));

        await Criar().AnexarArquivoAsync(Tarefa, "log.txt", "text/plain", conteudo, TestContext.Current.CancellationToken);

        Assert.Equal(2, _asana.Requisicoes.Count);
        Assert.All(_asana.Requisicoes, requisicao => Assert.Contains("linha do log", requisicao.Corpo));
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "O Asana recusou o arquivo por ser grande demais.")]
    [InlineData(HttpStatusCode.BadRequest, "O Asana recusou o comentário ou o anexo. Revise o conteúdo e tente novamente.")]
    [InlineData(HttpStatusCode.NotFound, "A tarefa não existe mais no Asana.")]
    public async Task RecusaDoAnexoNaoERepetidaAsync(HttpStatusCode status, string mensagem)
    {
        _asana.Responder(status, new { errors = new[] { new { message = "rejected" } } });
        using var conteudo = new MemoryStream([1, 2, 3]);

        var erro = await Assert.ThrowsAsync<AsanaRequisicaoRecusadaException>(
            () => Criar().AnexarArquivoAsync(Tarefa, "grande.zip", "application/zip", conteudo, TestContext.Current.CancellationToken));

        Assert.Equal(mensagem, erro.Message);
        Assert.Single(_asana.Requisicoes);
    }

    [Fact]
    public async Task ComentarioLevaMencoesTextoEscapadoEQuebrasDeLinhaAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new
        {
            data = new
            {
                gid = "s1",
                resource_subtype = "comment_added",
                created_at = "2026-11-03T21:15:00.000Z",
                created_by = new { gid = "7", name = "Ricardo" },
                text = "Maria Angelica Pode verificar <se> a & b \"ok\"?\nObrigado",
            },
        });

        var story = await Criar().ComentarAsync(Tarefa, "Pode verificar <se> a & b \"ok\"?\nObrigado", ["9"], TestContext.Current.CancellationToken);

        var requisicao = Assert.Single(_asana.Requisicoes);
        Assert.Equal($"https://app.asana.com/api/1.0/tasks/{Tarefa}/stories?opt_fields=resource_subtype,created_at,created_by.name,text", requisicao.Uri.ToString());
        using var json = JsonDocument.Parse(requisicao.Corpo!);
        Assert.Equal(
            "<body><a data-asana-gid=\"9\"/> Pode verificar &lt;se&gt; a &amp; b &quot;ok&quot;?\nObrigado</body>",
            json.RootElement.GetProperty("data").GetProperty("html_text").GetString());
        Assert.Equal(("s1", AsanaStoryTipo.Comentario, "Ricardo"), (story.Gid, story.Tipo, story.AutorNome));
        Assert.Equal("Maria Angelica Pode verificar <se> a & b \"ok\"?\nObrigado", story.Texto);
    }

    [Fact]
    public void ComentarioSemTextoTemSoAsMencoes()
    {
        Assert.Equal("<body><a data-asana-gid=\"9\"/> <a data-asana-gid=\"12\"/></body>", AsanaClient.HtmlDoComentario(string.Empty, ["9", "12"]));
        Assert.Equal("<body>Só texto</body>", AsanaClient.HtmlDoComentario("Só texto", []));
    }

    [Fact]
    public async Task ComentarioRecusadoViraRequisicaoRecusadaEOTokenNaoVaiParaOLogAsync()
    {
        _asana.Responder(HttpStatusCode.BadRequest, new { errors = new[] { new { message = "Invalid html_text" } } });

        await Assert.ThrowsAsync<AsanaRequisicaoRecusadaException>(
            () => Criar().ComentarAsync(Tarefa, "texto", [], TestContext.Current.CancellationToken));

        Assert.NotEmpty(_logger.Mensagens);
        Assert.DoesNotContain(_logger.Mensagens, mensagem => mensagem.Contains(Token, StringComparison.Ordinal));
    }

    private AsanaClient Criar()
    {
        var httpClient = new HttpClient(_asana);
        AsanaClient.Configurar(httpClient);

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AsanaClient.TokenKey] = Token })
            .Build();

        return new AsanaClient(httpClient, configuracao, _logger);
    }

    /// <summary>
    /// Logger que guarda as mensagens formatadas
    /// </summary>
    private sealed class LoggerDeTeste : ILogger<AsanaClient>
    {
        public List<string> Mensagens { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Mensagens.Add(formatter(state, exception));
        }
    }
}
