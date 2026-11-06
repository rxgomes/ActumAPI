using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Domain;
using Actum.Modules.Tarefas.Persistence;
using Actum.Tests.Shared;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários de "Bases recentes da empresa" (<c>task-lifecycle</c>), com duas empresas e o GitHub simulado.
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class BasesRecentesTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP", "aremas.com.br")
{
    private const string Repositorio = "ixact/erp-api";

    private readonly GitHubClientFalso _gitHub = new();

    [Fact]
    public async Task DevolveAsTresBasesDistintasMaisRecentesDaEmpresaAsync()
    {
        await CarregarAsync(Workspace, "a", "b", "c");

        // Ordem de registro: main, release/2026-11, main, develop, hotfix.
        await RegistrarBaseAsync("a", "main");
        await RegistrarBaseAsync("b", "release/2026-11");
        await RegistrarBaseAsync("c", "main");
        await RegistrarBaseAsync("a", "develop");
        await RegistrarBaseAsync("b", "hotfix");

        var tarefa = await IdentifierAsync("c");
        var bases = await TarefasAsync(service => service.ListarBasesRecentesAsync(tarefa, Ct));

        Assert.Equal(["hotfix", "develop", "main"], bases);
    }

    [Fact]
    public async Task BasesDeOutraEmpresaNaoAparecemAsync()
    {
        // As duas empresas na mesma sincronização: as duas tarefas entram pela carga inicial (em andamento).
        Abertas(Workspace, TarefaAsana("a"));
        Abertas(Workspaces[1].Gid, TarefaAsana("outra"));
        await SincronizarAsync();
        await DefinirRepositoriosAsync(Workspace, null, Repositorio);
        await DefinirRepositoriosAsync(Workspaces[1].Gid, null, Repositorio);
        await RegistrarBaseAsync("outra", "master");

        var tarefa = await IdentifierAsync("a");
        var bases = await TarefasAsync(service => service.ListarBasesRecentesAsync(tarefa, Ct));

        Assert.NotNull(bases);
        Assert.Empty(bases);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Tarefas da carga inicial do workspace (em andamento), com o repositório de back da empresa definido
    /// </summary>
    private async Task CarregarAsync(string workspace, params string[] nomes)
    {
        Abertas(workspace, [.. nomes.Select(nome => TarefaAsana(nome))]);
        await SincronizarAsync();
        await DefinirRepositoriosAsync(workspace, null, Repositorio);
    }

    private async Task RegistrarBaseAsync(string nome, string branch)
    {
        _gitHub.ComBranch(Repositorio, branch, $"sha-{branch}");

        var tarefa = await IdentifierAsync(nome);
        var resultado = await TarefasAsync(service => service.AdicionarBranchAsync(tarefa, new AdicionarBranchRequest(TipoBranch.Base, branch, [ParteBranch.Back]), Ct));
        Assert.Equal(TarefaResultadoStatus.Sucesso, resultado.Status);
    }

    private async Task<Guid> IdentifierAsync(string nome)
    {
        return (await LerTarefaAsync(nome))!.Identifier;
    }

    private async Task<T> TarefasAsync<T>(Func<ITarefaService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return await acao(new TarefaService(contexto, _gitHub));
    }
}
