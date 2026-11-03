using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Actum.Shared.Infrastructure.Tests.Convencoes;

/// <summary>
/// Cenários da capability <c>base-entity</c>.
/// </summary>
[Collection(BancoDeTesteCollection.Nome)]
public sealed class EntidadeBaseTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Tabela = "teste_convencoes.produto_teste";

    private const string TimestampSemTimezone = "timestamp without time zone";

    public async ValueTask InitializeAsync()
    {
        await using var contexto = TestModuleDbContext.Criar(postgres.DataSource);
        await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ColunasComunsSaoCriadasComTiposENotNullAsync()
    {
        await using var comando = postgres.DataSource.CreateCommand(
            "select column_name, data_type, is_nullable from information_schema.columns " +
            "where table_schema = 'teste_convencoes' and table_name = 'produto_teste'");

        var colunas = new Dictionary<string, (string Tipo, string Nulavel)>();
        await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await leitor.ReadAsync(TestContext.Current.CancellationToken))
        {
            colunas.Add(leitor.GetString(0), (leitor.GetString(1), leitor.GetString(2)));
        }

        Assert.Equal(("bigint", "NO"), colunas["id"]);
        Assert.Equal(("uuid", "NO"), colunas["identifier"]);
        Assert.Equal((TimestampSemTimezone, "NO"), colunas["create_in"]);
        Assert.Equal((TimestampSemTimezone, "NO"), colunas["update_in"]);
        Assert.Equal(("boolean", "NO"), colunas["is_active"]);
    }

    [Fact]
    public async Task IdEGeradoPeloBancoECrescenteAsync()
    {
        var primeiro = await IncluirAsync(new ProdutoTeste { Nome = "primeiro" });
        var segundo = await IncluirAsync(new ProdutoTeste { Nome = "segundo" });

        Assert.True(primeiro.Id > 0);
        Assert.True(segundo.Id > primeiro.Id);
    }

    [Fact]
    public async Task IdentifierEVersao7EIgualAoValorAntesDeSalvarAsync()
    {
        var produto = new ProdutoTeste { Nome = "identifier" };
        var identifierAntesDeSalvar = produto.Identifier;

        await IncluirAsync(produto);
        var lido = await LerAsync(produto.Id);

        Assert.Equal(7, lido.Identifier.Version);
        Assert.Equal(identifierAntesDeSalvar, lido.Identifier);
    }

    [Fact]
    public async Task IdentifierDuplicadoERejeitadoAsync()
    {
        var produto = await IncluirAsync(new ProdutoTeste { Nome = "original" });

        await using var comando = postgres.DataSource.CreateCommand(
            $"insert into {Tabela} (nome, identifier) values ('duplicado', $1)");
        comando.Parameters.Add(new NpgsqlParameter { Value = produto.Identifier });

        var erro = await Assert.ThrowsAsync<PostgresException>(
            () => comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, erro.SqlState);
    }

    [Fact]
    public async Task InclusaoPorSqlPreencheOsDefaultsAsync()
    {
        var inicio = await LerHoraDoBancoAsync();

        await using var comando = postgres.DataSource.CreateCommand(
            $"insert into {Tabela} (nome) values ('sql') returning identifier, create_in, update_in, is_active");
        await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await leitor.ReadAsync(TestContext.Current.CancellationToken);
        var identifier = leitor.GetGuid(0);
        var createIn = leitor.GetDateTime(1);
        var updateIn = leitor.GetDateTime(2);
        var isActive = leitor.GetBoolean(3);
        await leitor.DisposeAsync();

        var fim = await LerHoraDoBancoAsync();

        Assert.Equal(7, identifier.Version);
        Assert.InRange(createIn, inicio, fim);
        Assert.Equal(createIn, updateIn);
        Assert.True(isActive);
    }

    [Fact]
    public async Task InclusaoPreencheCreateInEUpdateInComAMesmaHoraAsync()
    {
        var antes = TruncarParaMicrossegundos(DateTime.Now);
        var produto = await IncluirAsync(new ProdutoTeste { Nome = "datas" });
        var depois = DateTime.Now;

        var lido = await LerAsync(produto.Id);

        Assert.InRange(lido.CreateIn, antes, depois);
        Assert.Equal(lido.CreateIn, lido.UpdateIn);
        Assert.Equal(TruncarParaMicrossegundos(produto.CreateIn), lido.CreateIn);
    }

    [Fact]
    public async Task AlteracaoAtualizaUpdateInEPreservaCreateInAsync()
    {
        var produto = await IncluirAsync(new ProdutoTeste { Nome = "antes" });
        var original = await LerAsync(produto.Id);
        await Task.Delay(10, TestContext.Current.CancellationToken);

        await using (var contexto = TestModuleDbContext.Criar(postgres.DataSource))
        {
            var rastreado = await contexto.Set<ProdutoTeste>().SingleAsync(p => p.Id == produto.Id, TestContext.Current.CancellationToken);
            rastreado.Nome = "depois";
            contexto.Entry(rastreado).Property(p => p.CreateIn).CurrentValue = new DateTime(2000, 1, 1);
            await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var alterado = await LerAsync(produto.Id);

        Assert.Equal("depois", alterado.Nome);
        Assert.Equal(original.CreateIn, alterado.CreateIn);
        Assert.True(alterado.UpdateIn > alterado.CreateIn);
    }

    [Fact]
    public async Task SalvarSemAlteracaoNaoMudaUpdateInAsync()
    {
        var produto = await IncluirAsync(new ProdutoTeste { Nome = "sem alteracao" });
        var original = await LerAsync(produto.Id);
        await Task.Delay(10, TestContext.Current.CancellationToken);

        await using (var contexto = TestModuleDbContext.Criar(postgres.DataSource))
        {
            await contexto.Set<ProdutoTeste>().SingleAsync(p => p.Id == produto.Id, TestContext.Current.CancellationToken);
            await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var contexto = TestModuleDbContext.Criar(postgres.DataSource))
        {
            var rastreado = await contexto.Set<ProdutoTeste>().SingleAsync(p => p.Id == produto.Id, TestContext.Current.CancellationToken);
            contexto.Entry(rastreado).Property(p => p.CreateIn).CurrentValue = new DateTime(2000, 1, 1);
            await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var lido = await LerAsync(produto.Id);

        Assert.Equal(original.CreateIn, lido.CreateIn);
        Assert.Equal(original.UpdateIn, lido.UpdateIn);
    }

    [Fact]
    public async Task IsActiveEVerdadeiroPorPadraoAsync()
    {
        var produto = await IncluirAsync(new ProdutoTeste { Nome = "ativo" });

        var lido = await LerAsync(produto.Id);

        Assert.True(lido.IsActive);
    }

    [Fact]
    public async Task IsActiveFalsoEPersistidoAsync()
    {
        var produto = await IncluirAsync(new ProdutoTeste { Nome = "inativo", IsActive = false });

        var lido = await LerAsync(produto.Id);

        Assert.False(lido.IsActive);
    }

    private async Task<ProdutoTeste> IncluirAsync(ProdutoTeste produto)
    {
        await using var contexto = TestModuleDbContext.Criar(postgres.DataSource);
        contexto.Add(produto);
        await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);

        return produto;
    }

    private async Task<ProdutoTeste> LerAsync(long id)
    {
        await using var contexto = TestModuleDbContext.Criar(postgres.DataSource);

        return await contexto.Set<ProdutoTeste>().AsNoTracking().SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<DateTime> LerHoraDoBancoAsync()
    {
        await using var comando = postgres.DataSource.CreateCommand("select localtimestamp");

        return (DateTime)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static DateTime TruncarParaMicrossegundos(DateTime valor)
    {
        return new DateTime(valor.Ticks - (valor.Ticks % 10), valor.Kind);
    }
}
