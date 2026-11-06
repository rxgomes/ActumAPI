using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Actum.Modules.Asana.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Modules.Tarefas.Tests;
using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Actum.Api.Tests;

/// <summary>
/// Cenários de <c>task-lifecycle</c>, <c>company-repositories</c> e <c>github-client</c> que dependem do pipeline
/// HTTP: validação dos DTOs, tradução de <c>DomainException</c>, de conflito e das falhas do GitHub, e formato JSON
/// da resposta. O GitHub é simulado por <see cref="GitHubClientFalso"/>.
/// </summary>
[Collection(TarefasMigradasCollection.Nome)]
public sealed class TarefaApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string ProblemJson = "application/problem+json";

    private const string RepositorioFront = "ixact/erp-web";

    private const string RepositorioBack = "ixact/erp-api";

    private static readonly string[] FrontEBack = ["front", "back"];

    private readonly GitHubClientFalso _gitHub = new GitHubClientFalso()
        .ComBranch(RepositorioFront, "main", "sha-front")
        .ComBranch(RepositorioBack, "main", "sha-back");

    private Guid _tarefa;

    private Guid _empresa;

    public async ValueTask InitializeAsync()
    {
        await using var contexto = TarefasDbContext.Criar(postgres.DataSource);
        await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var sufixo = Guid.NewGuid().ToString("N");
        var empresa = new Empresa($"ws-{sufixo}", "Aureum-ERP", "#1976D2")
        {
            RepositorioFront = RepositorioFront,
            RepositorioBack = RepositorioBack,
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
        Assert.Equal(["Informe se a branch é do front, do back ou dos dois."], erros.GetProperty("Partes").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task InicioComPartesVaziasRetornaErroNoCampoAsync()
    {
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "feature/ipi-devolvido", prazoInterno = "2026-11-10", partes = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(["Informe se a branch é do front, do back ou dos dois."], corpo.GetProperty("errors").GetProperty("Partes").EnumerateArray().Select(e => e.GetString()));
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task InicioComBranchesIguaisRetorna400ComMensagemAsync()
    {
        var (status, corpo, tipo) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "main", prazoInterno = "2026-11-10", partes = FrontEBack });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("A branch base e a branch de implementação devem ser diferentes.", corpo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InicioValidoRespondeStatusEPartesEmSnakeCaseComATagAsync()
    {
        var (status, corpo, _) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "feature/ipi-devolvido", prazoInterno = "2026-11-10", partes = FrontEBack });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("em_andamento", corpo.GetProperty("status").GetString());
        Assert.Equal("2026-11-10", corpo.GetProperty("prazoInterno").GetString());
        Assert.Equal(
            [("base", "main", false), ("implementacao", "feature/ipi-devolvido", false)],
            corpo.GetProperty("branches").EnumerateArray().Select(b => (b.GetProperty("tipo").GetString(), b.GetProperty("nome").GetString(), b.GetProperty("enviada").GetBoolean())));
        Assert.All(corpo.GetProperty("branches").EnumerateArray(), b => Assert.Equal(FrontEBack, b.GetProperty("partes").EnumerateArray().Select(p => p.GetString())));
        Assert.Equal(
            ["(Front, Back) => main", "(Front, Back) => feature/ipi-devolvido"],
            corpo.GetProperty("branches").EnumerateArray().Select(b => b.GetProperty("tag").GetString()));

        var (statusDeNovo, corpoDeNovo, _) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "outra", prazoInterno = "2026-11-10", partes = FrontEBack });
        Assert.Equal(HttpStatusCode.BadRequest, statusDeNovo);
        Assert.Contains("Status atual: em andamento", corpoDeNovo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task BaseInexistenteNoGitHubRetorna400ComMensagemAsync()
    {
        var (status, corpo, tipo) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "release/2026-12", branchImplementacao = "feature/ipi-devolvido", prazoInterno = "2026-11-10", partes = new[] { "back" } });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal($"A branch base 'release/2026-12' não existe no repositório de back ({RepositorioBack}).", corpo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GitHubForaDoArRetorna502Async()
    {
        _gitHub.FalharNaCriacaoEm = RepositorioBack;

        var (status, corpo, tipo) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "feature/ipi-devolvido", prazoInterno = "2026-11-10", partes = new[] { "back" } });

        Assert.Equal(HttpStatusCode.BadGateway, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("Não foi possível comunicar com o GitHub. Tente novamente em instantes.", corpo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task BasesRecentesDevolvemListaE404ParaTarefaInexistenteAsync()
    {
        var (vazio, nenhuma, _) = await EnviarAsync(HttpMethod.Get, $"/tarefas/{_tarefa}/branches-base-recentes", null);
        Assert.Equal(HttpStatusCode.OK, vazio);
        Assert.Equal(0, nenhuma.GetArrayLength());

        await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "feature/ipi-devolvido", prazoInterno = "2026-11-10", partes = FrontEBack });

        var (status, bases, _) = await EnviarAsync(HttpMethod.Get, $"/tarefas/{_tarefa}/branches-base-recentes", null);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(["main"], bases.EnumerateArray().Select(b => b.GetString()));

        var (statusInexistente, _, tipo) = await EnviarAsync(HttpMethod.Get, $"/tarefas/{Guid.CreateVersion7()}/branches-base-recentes", null);
        Assert.Equal(HttpStatusCode.NotFound, statusInexistente);
        Assert.Equal(ProblemJson, tipo);
    }

    [Fact]
    public async Task RepositoriosDaEmpresaSaoAlteradosEFormatoInvalidoRetornaErroNoCampoAsync()
    {
        var (status, corpo, _) = await EnviarAsync(HttpMethod.Patch, $"/empresas/{_empresa}", new { repositorioFront = "  ixact/erp-web2 ", repositorioBack = "" });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("ixact/erp-web2", corpo.GetProperty("repositorioFront").GetString());
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("repositorioBack").ValueKind);

        var (statusSoCor, soCor, _) = await EnviarAsync(HttpMethod.Patch, $"/empresas/{_empresa}", new { cor = "#388E3C" });
        Assert.Equal(HttpStatusCode.OK, statusSoCor);
        Assert.Equal("ixact/erp-web2", soCor.GetProperty("repositorioFront").GetString());

        var (statusInvalido, invalido, _) = await EnviarAsync(HttpMethod.Patch, $"/empresas/{_empresa}", new { repositorioFront = "https://github.com/ixact/erp-web" });
        Assert.Equal(HttpStatusCode.BadRequest, statusInvalido);
        Assert.Equal(
            ["O repositório de front deve estar no formato dono/repo (ex.: ixact/erp-web)."],
            invalido.GetProperty("errors").GetProperty("RepositorioFront").EnumerateArray().Select(e => e.GetString()));
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
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "feature/ipi-devolvido", prazoInterno = "2026-11-10", partes = FrontEBack });

        var (criada, branch, _) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/branches", new { tipo = "implementacao", nome = "fix/pagamento", partes = new[] { "front" } });
        Assert.Equal(HttpStatusCode.Created, criada);
        Assert.Equal("implementacao", branch.GetProperty("tipo").GetString());
        Assert.Equal("(Front) => fix/pagamento", branch.GetProperty("tag").GetString());

        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/branches", new { tipo = "implementacao", nome = "fix/pagamento", partes = new[] { "front" } });
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

    [Fact]
    public async Task DetalheSugereNovaBranchNoRetrabalhoERetomarComCorpoCriaABranchAsync()
    {
        var tarefa = await EmRetrabalhoAsync();

        var (status, detalhe, _) = await EnviarAsync(HttpMethod.Get, $"/tarefas/{tarefa}", null);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("retrabalho", detalhe.GetProperty("status").GetString());
        Assert.True(detalhe.GetProperty("novaBranchSugerida").GetBoolean());

        var (statusRetomada, retomada, _) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{tarefa}/retomar", new { branchBase = "main", branchImplementacao = "261106-ajuste-ipi", partes = new[] { "back" } });
        Assert.Equal(HttpStatusCode.OK, statusRetomada);
        Assert.True(retomada.GetProperty("emAndamento").GetBoolean());
        Assert.Contains(
            ("261106-ajuste-ipi", "(Back) => 261106-ajuste-ipi", (short?)1),
            retomada.GetProperty("branches").EnumerateArray().Select(b => (b.GetProperty("nome").GetString(), b.GetProperty("tag").GetString(), RetrabalhoNumero(b))));

        var (_, depois, _) = await EnviarAsync(HttpMethod.Get, $"/tarefas/{tarefa}", null);
        Assert.False(depois.GetProperty("novaBranchSugerida").GetBoolean());
    }

    [Fact]
    public async Task RetomarSemCorpoContinuaRespondendo200Async()
    {
        await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{_tarefa}/iniciar", new { branchBase = "main", branchImplementacao = "feature/ipi-devolvido", prazoInterno = "2026-11-10", partes = FrontEBack });
        await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/pausar", null);
        _gitHub.Chamadas.Clear();

        var (status, corpo, _) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/retomar", null);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(corpo.GetProperty("emAndamento").GetBoolean());
        Assert.Equal(2, corpo.GetProperty("branches").GetArrayLength());
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task RetomarComCorpoIncompletoRetornaErrosPorCampoAsync()
    {
        var (status, corpo, tipo) = await EnviarAsync(HttpMethod.Post, $"/tarefas/{_tarefa}/retomar", new { branchBase = "main", partes = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(ProblemJson, tipo);
        var erros = corpo.GetProperty("errors");
        Assert.Equal(["A branch de implementação é obrigatória."], erros.GetProperty("BranchImplementacao").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["Informe se a branch é do front, do back ou dos dois."], erros.GetProperty("Partes").EnumerateArray().Select(e => e.GetString()));
        Assert.Empty(_gitHub.Chamadas);
    }

    [Fact]
    public async Task RetomarComImplementacaoRepetidaRetorna409Async()
    {
        var tarefa = await EmRetrabalhoAsync();

        var (status, corpo, tipo) = await EnviarAsync(
            HttpMethod.Post, $"/tarefas/{tarefa}/retomar", new { branchBase = "main", branchImplementacao = "feature/ipi-devolvido", partes = FrontEBack });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal(ProblemJson, tipo);
        Assert.Equal("A tarefa já tem uma branch com esse nome.", corpo.GetProperty("detail").GetString());
    }

    private static short? RetrabalhoNumero(JsonElement branch)
    {
        var numero = branch.GetProperty("retrabalhoNumero");
        return numero.ValueKind == JsonValueKind.Null ? null : numero.GetInt16();
    }

    /// <summary>
    /// Tarefa da empresa do teste iniciada com <c>main</c> e <c>feature/ipi-devolvido</c>, concluída e reaberta no
    /// histórico do Asana: em retrabalho, pausada, sem implementação vinculada ao retrabalho
    /// </summary>
    private async Task<Guid> EmRetrabalhoAsync()
    {
        await using var contexto = TarefasDbContext.Criar(postgres.DataSource);
        var empresa = await contexto.Set<Empresa>().SingleAsync(e => e.Identifier == _empresa, TestContext.Current.CancellationToken);

        var sufixo = Guid.NewGuid().ToString("N");
        var asana = new AsanaTarefa($"r-{sufixo}", "Tarefa reaberta", "", "", null, false, DateTime.Now, "", [], [], "user");
        var tarefa = Tarefa.Criar(empresa, asana, cargaInicial: false, DateTime.Now);
        tarefa.Iniciar("main", "feature/ipi-devolvido", new DateOnly(2026, 11, 10), [ParteBranch.Front, ParteBranch.Back]);

        var concluidaEm = DateTime.Now.AddHours(-2);
        tarefa.MarcarConcluida(concluidaEm);
        tarefa.RecalcularRetrabalhos(
            [
                new AsanaStory($"c-{sufixo}", AsanaStoryTipo.MarcadaConcluida, concluidaEm, "Ricardo", "Ricardo concluiu esta tarefa"),
                new AsanaStory($"i-{sufixo}", AsanaStoryTipo.MarcadaIncompleta, concluidaEm.AddHours(1), "Maria", "Maria marcou como incompleta"),
            ],
            voltouParaALista: true);
        Assert.Equal(StatusTarefa.Retrabalho, tarefa.Status);

        contexto.Add(tarefa);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        return tarefa.Identifier;
    }

    private async Task<(HttpStatusCode Status, JsonElement Corpo, string? Tipo)> EnviarAsync(HttpMethod metodo, string caminho, object? corpo)
    {
        await using var factory = new ActumApiFactory(Environments.Production, postgres.ConnectionString, gitHub: _gitHub);
        using var cliente = factory.CreateClient();

        using var requisicao = new HttpRequestMessage(metodo, caminho) { Content = corpo is null ? null : JsonContent.Create(corpo) };
        using var resposta = await cliente.SendAsync(requisicao, TestContext.Current.CancellationToken);
        var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone(), resposta.Content.Headers.ContentType?.MediaType);
    }
}
