using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Git.Application;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Modules.Tarefas.Tests;
using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Actum.Api.Tests;

/// <summary>
/// Cenários de <c>task-worktrees</c> e de <c>company-repositories</c> (configuração local) que dependem do pipeline
/// HTTP: formato JSON do worktree, tradução do conflito de pendências em <c>409</c>, de não encontrado em <c>404</c> e
/// de <c>DomainException</c> em <c>400</c>. O GitHub e o git são simulados.
/// </summary>
[Collection(TarefasMigradasCollection.Nome)]
public sealed class WorktreeApiTests(PostgresFixture postgres) : IAsyncLifetime, IDisposable
{
    private const string Feature = "feature/ipi-devolvido";

    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"actum-wt-api-{Guid.NewGuid():N}");

    private readonly GitHubClientFalso _gitHub = new GitHubClientFalso()
        .ComBranch("ixact/erp-web", "main", "sha-front")
        .ComBranch("ixact/erp-api", "main", "sha-back");

    private readonly GitLocalFalso _git = new();

    private Guid _tarefa;

    private Guid _empresa;

    private string CloneFront => Path.Combine(_raiz, "erp-web");

    private string CloneBack => Path.Combine(_raiz, "erp-api");

    private string PastaWorktrees => Path.Combine(_raiz, "wt");

    public async ValueTask InitializeAsync()
    {
        await using var contexto = TarefasDbContext.Criar(postgres.DataSource);
        await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);

        Directory.CreateDirectory(CloneFront);
        Directory.CreateDirectory(CloneBack);
        _git.ComClone(CloneFront, "https://github.com/ixact/erp-web.git", Feature)
            .ComClone(CloneBack, "https://github.com/ixact/erp-api.git", Feature);

        var sufixo = Guid.NewGuid().ToString("N");
        var empresa = new Empresa($"ws-{sufixo}", "Aureum-ERP", "#1976D2")
        {
            RepositorioFront = "ixact/erp-web",
            RepositorioBack = "ixact/erp-api",
        };
        contexto.Add(empresa);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        var asana = new AsanaTarefa($"t-{sufixo}", "Tarefa", "", "", null, false, DateTime.Now, "", [], [], "user");
        var tarefa = Tarefa.Criar(empresa, asana, cargaInicial: false, DateTime.Now);
        contexto.Add(tarefa);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tarefa = tarefa.Identifier;
        _empresa = empresa.Identifier;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }
    }

    [Fact]
    public async Task ConfiguracaoLocalPeloPatchAsync()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Patch, $"/empresas/{_empresa}", new
        {
            cloneFront = CloneFront,
            cloneBack = CloneBack,
            pastaWorktrees = PastaWorktrees,
            arquivosFront = new[] { ".env.development" },
        });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(CloneFront, corpo.GetProperty("cloneFront").GetString());
        Assert.Equal(PastaWorktrees, corpo.GetProperty("pastaWorktrees").GetString());
        Assert.Equal([".env.development"], corpo.GetProperty("arquivosFront").EnumerateArray().Select(a => a.GetString()));
        Assert.Empty(corpo.GetProperty("arquivosBack").EnumerateArray());

        var (statusInvalido, invalido, _) = await EnviarAsync(HttpMethod.Patch, $"/empresas/{_empresa}", new { pastaWorktrees = "relativa" });
        Assert.Equal(HttpStatusCode.BadRequest, statusInvalido);
        Assert.StartsWith("A pasta dos worktrees deve ser um caminho absoluto", invalido.GetProperty("detail").GetString(), StringComparison.Ordinal);

        var (statusLista, lista, _) = await EnviarAsync(HttpMethod.Patch, $"/empresas/{_empresa}", new { arquivosBack = Enumerable.Range(1, 21).Select(n => $"a{n}.json") });
        Assert.Equal(HttpStatusCode.BadRequest, statusLista);
        Assert.Equal(["Informe no máximo 20 arquivos de back."], lista.GetProperty("errors").GetProperty("ArquivosBack").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task InicioCriaOWorktreeEDevolveEmSnakeCaseAsync()
    {
        await ConfigurarEmpresaAsync();

        var (status, corpo, _) = await IniciarAsync();

        Assert.Equal(HttpStatusCode.OK, status);
        var branches = corpo.GetProperty("branches").EnumerateArray().ToList();
        Assert.Equal(JsonValueKind.Null, branches[0].GetProperty("worktree").ValueKind);
        var worktree = branches[1].GetProperty("worktree");
        var pasta = Path.Combine(PastaWorktrees, "feature-ipi-devolvido");
        Assert.Equal(pasta, worktree.GetProperty("pasta").GetString());
        Assert.Equal("criado", worktree.GetProperty("situacao").GetString());
        Assert.Equal($"cd '{pasta}'; claude", worktree.GetProperty("comandoClaude").GetString());
        Assert.Equal(
            [("front", Path.Combine(pasta, "front"), "criado"), ("back", Path.Combine(pasta, "back"), "criado")],
            worktree.GetProperty("partes").EnumerateArray().Select(p => (p.GetProperty("parte").GetString(), p.GetProperty("caminho").GetString(), p.GetProperty("situacao").GetString())));
    }

    [Fact]
    public async Task RecriarRemoverEConflitoAsync()
    {
        await ConfigurarEmpresaAsync();
        _git.FalharEm(CloneBack, "buscar");
        var (_, iniciada, _) = await IniciarAsync();
        var branch = iniciada.GetProperty("branches")[1];
        Assert.Equal("falhou", branch.GetProperty("worktree").GetProperty("situacao").GetString());
        Assert.Contains("falha simulada", branch.GetProperty("worktree").GetProperty("partes")[1].GetProperty("erro").GetString(), StringComparison.Ordinal);
        var caminho = $"/tarefas/{_tarefa}/branches/{branch.GetProperty("identifier").GetGuid()}/worktree";

        _git.NaoFalharMais(CloneBack, "buscar");
        var (statusRecriado, recriado, _) = await EnviarAsync(HttpMethod.Post, caminho, null);
        Assert.Equal(HttpStatusCode.OK, statusRecriado);
        Assert.Equal("criado", recriado.GetProperty("worktree").GetProperty("situacao").GetString());

        _git.Pendencias[Path.Combine(PastaWorktrees, "feature-ipi-devolvido", "back")] = new PendenciasDoWorktree(0, 2);
        var (statusConflito, conflito, tipo) = await EnviarAsync(HttpMethod.Delete, caminho, null);
        Assert.Equal(HttpStatusCode.Conflict, statusConflito);
        Assert.Equal("application/problem+json", tipo);
        Assert.Contains("back: 2 commits não enviados", conflito.GetProperty("detail").GetString(), StringComparison.Ordinal);

        _git.Pendencias.Clear();
        var (statusRemovido, removido, _) = await EnviarAsync(HttpMethod.Delete, caminho, null);
        Assert.Equal(HttpStatusCode.OK, statusRemovido);
        Assert.Equal("removido", removido.GetProperty("worktree").GetProperty("situacao").GetString());

        var (statusDeNovo, _, _) = await EnviarAsync(HttpMethod.Delete, caminho, null);
        Assert.Equal(HttpStatusCode.NotFound, statusDeNovo);
    }

    [Fact]
    public async Task CriarSemConfiguracaoEBranchBaseRespondem400Async()
    {
        var (_, iniciada, _) = await IniciarAsync();
        var (baseDaTarefa, implementacao) = (iniciada.GetProperty("branches")[0], iniciada.GetProperty("branches")[1]);

        var (statusSemConfiguracao, semConfiguracao, _) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/branches/{implementacao.GetProperty("identifier").GetGuid()}/worktree", null);
        Assert.Equal(HttpStatusCode.BadRequest, statusSemConfiguracao);
        Assert.Contains("na página Empresas", semConfiguracao.GetProperty("detail").GetString(), StringComparison.Ordinal);

        var (statusBase, _, _) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/branches/{baseDaTarefa.GetProperty("identifier").GetGuid()}/worktree", null);
        Assert.Equal(HttpStatusCode.BadRequest, statusBase);

        var (statusInexistente, _, _) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/branches/{Guid.CreateVersion7()}/worktree", null);
        Assert.Equal(HttpStatusCode.NotFound, statusInexistente);
    }

    private Task ConfigurarEmpresaAsync()
    {
        return EnviarAsync(HttpMethod.Patch, $"/empresas/{_empresa}", new { cloneFront = CloneFront, cloneBack = CloneBack, pastaWorktrees = PastaWorktrees });
    }

    private Task<(HttpStatusCode Status, JsonElement Corpo, string? Tipo)> IniciarAsync()
    {
        return EnviarAsync(
            HttpMethod.Post,
            $"/tarefas/{_tarefa}/iniciar",
            new { branchBase = "main", branchImplementacao = Feature, prazoInterno = "2026-11-20", partes = new[] { "front", "back" } });
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, string? Tipo)> EnviarAsync(HttpMethod metodo, string caminho, object? corpo)
    {
        await using var factory = new ActumApiFactory(Environments.Production, postgres.ConnectionString, gitHub: _gitHub, git: _git);
        using var cliente = factory.CreateClient();

        using var requisicao = new HttpRequestMessage(metodo, caminho) { Content = corpo is null ? null : JsonContent.Create(corpo) };
        using var resposta = await cliente.SendAsync(requisicao, TestContext.Current.CancellationToken);
        var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone(), resposta.Content.Headers.ContentType?.MediaType);
    }
}
