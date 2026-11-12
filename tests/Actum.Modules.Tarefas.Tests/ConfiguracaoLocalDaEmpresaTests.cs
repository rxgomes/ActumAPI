using Actum.Modules.Tarefas.Application;
using Actum.Modules.Tarefas.Contracts;
using Actum.Modules.Tarefas.Persistence;
using Actum.Shared.Abstractions.Domain;
using Actum.Tests.Shared;

namespace Actum.Modules.Tarefas.Tests;

/// <summary>
/// Cenários de "Configuração local da empresa" (<c>company-repositories</c>), com o git simulado
/// </summary>
[Collection(SincronizacaoCollection.Nome)]
public sealed class ConfiguracaoLocalDaEmpresaTests(PostgresFixture postgres) : SincronizacaoTestBase(postgres, "Aureum-ERP")
{
    private const string RepositorioFront = "ixact/erp-web";

    private const string RepositorioBack = "ixact/erp-api";

    private const string CloneFront = @"E:\src\aureum\erp-web";

    private const string CloneBack = @"E:\src\aureum\erp-api";

    private readonly GitLocalFalso _git = new GitLocalFalso()
        .ComClone(CloneFront, "https://github.com/ixact/erp-web.git")
        .ComClone(CloneBack, "git@github.com:Ixact/ERP-API.git");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EmpresaNovaComecaSemConfiguracaoLocalAsync()
    {
        await SincronizarAsync();

        var identifier = (await LerEmpresaAsync(Workspace)).Identifier;
        var empresa = Assert.Single(await EmpresasAsync(service => service.ListarAsync(Ct)), e => e.Identifier == identifier);

        Assert.Null(empresa.CloneFront);
        Assert.Null(empresa.CloneBack);
        Assert.Null(empresa.PastaWorktrees);
        Assert.Empty(empresa.ArquivosFront);
        Assert.Empty(empresa.ArquivosBack);
    }

    [Fact]
    public async Task DefinirAConfiguracaoLocalAsync()
    {
        var empresa = await EmpresaComRepositoriosAsync();

        var alterada = await AlterarAsync(empresa, new AlterarEmpresaRequest(
            null,
            null,
            CloneFront: $"  {CloneFront}\\ ",
            CloneBack: CloneBack,
            PastaWorktrees: @"E:\wt\aureum",
            ArquivosFront: [" .env.development ", ""],
            ArquivosBack: [@"src\Api\appsettings.Development.json"]));

        Assert.Equal(CloneFront, alterada.CloneFront);
        Assert.Equal(CloneBack, alterada.CloneBack);
        Assert.Equal(@"E:\wt\aureum", alterada.PastaWorktrees);
        Assert.Equal([".env.development"], alterada.ArquivosFront);
        Assert.Equal([@"src\Api\appsettings.Development.json"], alterada.ArquivosBack);

        var lida = await LerEmpresaAsync(Workspace);
        Assert.Equal(CloneFront, lida.CloneFront);
        Assert.Equal([".env.development"], lida.ArquivosFront);
    }

    [Fact]
    public async Task CloneDeOutroRepositorioAsync()
    {
        var empresa = await EmpresaComRepositoriosAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, CloneBack: CloneFront)));

        Assert.Equal($@"O clone de back ({CloneFront}) aponta para ixact/erp-web, mas o repositório de back da empresa é {RepositorioBack}.", erro.Message);
    }

    [Fact]
    public async Task CloneSemRepositorioDoGitHubAsync()
    {
        await SincronizarAsync();
        var empresa = await LerEmpresaAsync(Workspace);

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => AlterarAsync(empresa.Identifier, new AlterarEmpresaRequest(null, null, CloneFront: CloneFront)));

        Assert.Contains("Cadastre o repositório de front", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PastaQueNaoEhRepositorioAsync()
    {
        var empresa = await EmpresaComRepositoriosAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, CloneFront: @"E:\outra\pasta")));

        Assert.Contains(@"E:\outra\pasta não é a raiz de um repositório git", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaminhoRelativoAsync()
    {
        var empresa = await EmpresaComRepositoriosAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, PastaWorktrees: @"wt\aureum")));

        Assert.StartsWith("A pasta dos worktrees deve ser um caminho absoluto", erro.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"..\segredos.json")]
    [InlineData(@"src\..\..\segredos.json")]
    [InlineData(@"C:\segredos.json")]
    [InlineData(@"\segredos.json")]
    public async Task ArquivoForaDoRepositorioAsync(string arquivo)
    {
        var empresa = await EmpresaComRepositoriosAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, ArquivosBack: [arquivo])));

        Assert.StartsWith("Os arquivos de back devem ser caminhos relativos", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArquivoRepetidoAsync()
    {
        var empresa = await EmpresaComRepositoriosAsync();

        var erro = await Assert.ThrowsAsync<DomainException>(
            () => AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, ArquivosFront: [".env.development", ".ENV.development"])));

        Assert.Contains("repetido", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CampoInvalidoNaoGravaOsOutrosAsync()
    {
        var empresa = await EmpresaComRepositoriosAsync();

        await Assert.ThrowsAsync<DomainException>(() => AlterarAsync(
            empresa, new AlterarEmpresaRequest("#abcdef", null, CloneFront: CloneFront, PastaWorktrees: "relativa")));

        var lida = await LerEmpresaAsync(Workspace);
        Assert.Null(lida.CloneFront);
        Assert.NotEqual("#ABCDEF", lida.Cor);
    }

    [Fact]
    public async Task RemoverOCloneMantemOsDemaisAsync()
    {
        var empresa = await EmpresaComRepositoriosAsync();
        await AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, CloneFront: CloneFront, CloneBack: CloneBack, ArquivosBack: ["a.json"]));
        _git.Chamadas.Clear();

        var alterada = await AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, CloneBack: ""));

        Assert.Null(alterada.CloneBack);
        Assert.Equal(CloneFront, alterada.CloneFront);
        Assert.Equal(["a.json"], alterada.ArquivosBack);
        Assert.Empty(_git.Chamadas);
    }

    [Fact]
    public async Task ListaVaziaLimpaOsArquivosAsync()
    {
        var empresa = await EmpresaComRepositoriosAsync();
        await AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, ArquivosFront: [".env.development"]));

        var alterada = await AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, ArquivosFront: []));

        Assert.Empty(alterada.ArquivosFront);
    }

    [Fact]
    public async Task TrocarORepositorioConfereOCloneDeNovoAsync()
    {
        var empresa = await EmpresaComRepositoriosAsync();
        await AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, CloneFront: CloneFront));

        await Assert.ThrowsAsync<DomainException>(
            () => AlterarAsync(empresa, new AlterarEmpresaRequest(null, null, RepositorioFront: "ixact/outro-web")));
    }

    [Theory]
    [InlineData("https://github.com/ixact/erp-web.git", "ixact/erp-web")]
    [InlineData("https://github.com/ixact/erp-web", "ixact/erp-web")]
    [InlineData("git@github.com:ixact/erp.api.git", "ixact/erp.api")]
    [InlineData("https://usuario@github.com/ixact/erp-web/", "ixact/erp-web")]
    [InlineData("https://gitlab.com/ixact/erp-web.git", null)]
    public void RepositorioDaUrl(string url, string? repositorio)
    {
        Assert.Equal(repositorio, EmpresaService.RepositorioDaUrl(url));
    }

    private async Task<Guid> EmpresaComRepositoriosAsync()
    {
        await SincronizarAsync();
        await DefinirRepositoriosAsync(Workspace, RepositorioFront, RepositorioBack);
        return (await LerEmpresaAsync(Workspace)).Identifier;
    }

    private async Task<EmpresaResponse> AlterarAsync(Guid empresa, AlterarEmpresaRequest request)
    {
        var alterada = await EmpresasAsync(service => service.AlterarAsync(empresa, request, Ct));
        return Assert.IsType<EmpresaResponse>(alterada);
    }

    private async Task<T> EmpresasAsync<T>(Func<IEmpresaService, Task<T>> acao)
    {
        await using var contexto = TarefasDbContext.Criar(Postgres.DataSource);
        return await acao(new EmpresaService(contexto, _git));
    }
}
