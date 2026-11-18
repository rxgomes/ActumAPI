using System.Net;
using Actum.Modules.Asana.Application;
using Actum.Modules.Asana.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Actum.Modules.Asana.Tests;

/// <summary>
/// Cenários de leitura e download dos anexos de uma tarefa na capability <c>asana-client</c>, sem rede.
/// </summary>
public sealed class AnexosDoAsanaTests
{
    private const string Tarefa = "1210000000000001";

    private readonly HttpMessageHandlerFalso _asana = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnexosDaTarefaPercorremTodasAsPaginasAsync()
    {
        _asana
            .Responder(HttpStatusCode.OK, new
            {
                data = new object[] { new { gid = "a1", name = "print.png", created_at = "2026-11-03T13:00:00.000Z", size = 512, host = "asana" } },
                next_page = new { offset = "pagina2" },
            })
            .Responder(HttpStatusCode.OK, new
            {
                data = new object[] { new { gid = "a2", name = "planilha", created_at = "2026-11-03T14:00:00.000Z", size = (long?)null, host = "gdrive" } },
                next_page = (object?)null,
            });

        var anexos = await Criar().ListarAnexosAsync(Tarefa, Ct);

        Assert.Equal(
            [("a1", "print.png", 512L, "asana"), ("a2", "planilha", (long?)null, "gdrive")],
            anexos!.Select(anexo => (anexo.Gid, anexo.Nome, anexo.Tamanho, anexo.Hospedagem)));
        Assert.Equal(DateTimeKind.Unspecified, anexos![0].CriadoEm.Kind);
        Assert.Equal(
            $"https://app.asana.com/api/1.0/attachments?parent={Tarefa}&limit=100&opt_fields=name,created_at,size,host",
            _asana.Requisicoes[0].Uri.ToString());
        Assert.EndsWith("&offset=pagina2", _asana.Requisicoes[1].Uri.ToString());
    }

    [Fact]
    public async Task AnexosDeTarefaExcluidaSaoNulosAsync()
    {
        _asana.Responder(HttpStatusCode.NotFound, new { errors = new[] { new { message = "Not found" } } });

        Assert.Null(await Criar().ListarAnexosAsync(Tarefa, Ct));
    }

    [Fact]
    public async Task DownloadTrazEnderecoNovoETarefaDoAnexoAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new
        {
            data = new
            {
                gid = "a1",
                name = "print.png",
                download_url = "https://s3.amazonaws.com/asana/print.png?X-Amz-Expires=120",
                view_url = "https://app.asana.com/app/asana/-/get_asset?asset_id=a1",
                parent = new { gid = Tarefa, name = "Tarefa" },
            },
        });

        var download = await Criar().ObterDownloadDoAnexoAsync("a1", Ct);

        Assert.Equal(new AsanaDownloadDoAnexo("print.png", Tarefa, new Uri("https://s3.amazonaws.com/asana/print.png?X-Amz-Expires=120")), download);
        Assert.Equal("https://app.asana.com/api/1.0/attachments/a1?opt_fields=name,download_url,view_url,parent", _asana.Requisicoes.Single().Uri.ToString());
    }

    [Fact]
    public async Task ArquivoExternoSemDownloadUsaOEnderecoDeAberturaAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new
        {
            data = new { gid = "a2", name = "planilha", download_url = (string?)null, view_url = "https://docs.google.com/spreadsheets/d/x", parent = new { gid = Tarefa } },
        });

        var download = await Criar().ObterDownloadDoAnexoAsync("a2", Ct);

        Assert.Equal(new Uri("https://docs.google.com/spreadsheets/d/x"), download!.Endereco);
    }

    [Fact]
    public async Task EnderecoQueNaoEHttpsEIgnoradoAsync()
    {
        _asana.Responder(HttpStatusCode.OK, new
        {
            data = new { gid = "a3", name = "x", download_url = "javascript:alert(1)", view_url = "http://exemplo.com/x", parent = new { gid = Tarefa } },
        });

        Assert.Null((await Criar().ObterDownloadDoAnexoAsync("a3", Ct))!.Endereco);
    }

    [Fact]
    public async Task DownloadDeAnexoExcluidoENuloAsync()
    {
        _asana.Responder(HttpStatusCode.NotFound, new { errors = new[] { new { message = "Not found" } } });

        Assert.Null(await Criar().ObterDownloadDoAnexoAsync("a1", Ct));
    }

    private AsanaClient Criar()
    {
        var httpClient = new HttpClient(_asana);
        AsanaClient.Configurar(httpClient);

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AsanaClient.TokenKey] = "token-de-teste" })
            .Build();

        return new AsanaClient(httpClient, configuracao, NullLogger<AsanaClient>.Instance);
    }
}
