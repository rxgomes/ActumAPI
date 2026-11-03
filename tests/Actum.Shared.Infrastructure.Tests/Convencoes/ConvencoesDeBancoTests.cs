using Actum.Shared.Infrastructure.Persistence;
using Actum.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Actum.Shared.Infrastructure.Tests.Convencoes;

/// <summary>
/// Cenários da capability <c>database-conventions</c>.
/// </summary>
[Collection(BancoDeTesteCollection.Nome)]
public sealed class ConvencoesDeBancoTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string TimestampSemTimezone = "timestamp without time zone";

    public async ValueTask InitializeAsync()
    {
        await using var testModule = TestModuleDbContext.Criar(postgres.DataSource);
        await testModule.Database.MigrateAsync(TestContext.Current.CancellationToken);

        await using var outroModulo = OutroModuloDbContext.Criar(postgres.DataSource);
        await outroModulo.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task TabelaEColunasSaoCriadasEmSnakeCaseAsync()
    {
        var colunas = await ListarColunasAsync(TestModuleDbContext.SchemaName, "pedido_item");

        Assert.Equal(
            ["data_cancelamento", "data_criacao", "data_entrega", "id"],
            colunas.Keys.Order());
    }

    [Fact]
    public async Task ColunaPodeSerConsultadaSemAspasAsync()
    {
        await using var comando = postgres.DataSource.CreateCommand(
            "select data_criacao from teste_convencoes.pedido_item");

        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DateTimeEMapeadoParaTimestampSemTimezoneAsync()
    {
        var colunas = await ListarColunasAsync(TestModuleDbContext.SchemaName, "pedido_item");

        Assert.Equal(TimestampSemTimezone, colunas["data_criacao"]);
        Assert.Equal(TimestampSemTimezone, colunas["data_cancelamento"]);
    }

    [Fact]
    public async Task DateOnlyEMapeadoParaDateAsync()
    {
        var colunas = await ListarColunasAsync(TestModuleDbContext.SchemaName, "pedido_item");

        Assert.Equal("date", colunas["data_entrega"]);
    }

    [Fact]
    public async Task NenhumaColunaUsaTimestampComTimezoneAsync()
    {
        await using var comando = postgres.DataSource.CreateCommand(
            "select count(*) from information_schema.columns " +
            "where table_schema like 'teste\\_convencoes%' and data_type = 'timestamp with time zone'");

        var quantidade = (long)(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(0, quantidade);
    }

    [Fact]
    public async Task DateTimeGravadoELidoSemAlteracaoAsync()
    {
        var dataCriacao = new DateTime(2026, 10, 24, 15, 30, 45, 123, DateTimeKind.Unspecified);
        var item = new PedidoItem
        {
            DataCriacao = dataCriacao,
            DataEntrega = new DateOnly(2026, 11, 1),
            DataCancelamento = DateTime.Now,
        };

        await using (var contexto = TestModuleDbContext.Criar(postgres.DataSource))
        {
            contexto.Add(item);
            await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var contexto = TestModuleDbContext.Criar(postgres.DataSource))
        {
            var lido = await contexto.Set<PedidoItem>().SingleAsync(p => p.Id == item.Id, TestContext.Current.CancellationToken);

            Assert.Equal(dataCriacao, lido.DataCriacao);
            Assert.Equal(item.DataEntrega, lido.DataEntrega);
            Assert.Equal(TruncarParaMicrossegundos(item.DataCancelamento!.Value), lido.DataCancelamento);
        }
    }

    [Fact]
    public async Task TabelasFicamNoSchemaDoProprioModuloAsync()
    {
        Assert.Equal([TestModuleDbContext.SchemaName], await ListarSchemasDaTabelaAsync("pedido_item"));
        Assert.Equal([OutroModuloDbContext.SchemaName], await ListarSchemasDaTabelaAsync("cliente_cadastro"));
    }

    [Fact]
    public async Task CadaModuloTemSeuProprioHistoricoDeMigrationsAsync()
    {
        var historicoTestModule = await ListarMigrationsAplicadasAsync(TestModuleDbContext.SchemaName);
        var historicoOutroModulo = await ListarMigrationsAplicadasAsync(OutroModuloDbContext.SchemaName);

        await using var testModule = TestModuleDbContext.Criar(postgres.DataSource);
        await using var outroModulo = OutroModuloDbContext.Criar(postgres.DataSource);

        Assert.Equal(testModule.Database.GetMigrations(), historicoTestModule);
        Assert.Equal(outroModulo.Database.GetMigrations(), historicoOutroModulo);
        Assert.Empty(historicoTestModule.Intersect(historicoOutroModulo));
    }

    [Fact]
    public async Task HistoricoDeMigrationsUsaSnakeCaseAsync()
    {
        var colunas = await ListarColunasAsync(TestModuleDbContext.SchemaName, DatabaseServiceCollectionExtensions.MigrationsHistoryTableName);

        Assert.Equal(["migration_id", "product_version"], colunas.Keys.Order());
    }

    private async Task<Dictionary<string, string>> ListarColunasAsync(string schema, string tabela)
    {
        await using var comando = postgres.DataSource.CreateCommand(
            "select column_name, data_type from information_schema.columns where table_schema = $1 and table_name = $2");
        comando.Parameters.Add(new NpgsqlParameter { Value = schema });
        comando.Parameters.Add(new NpgsqlParameter { Value = tabela });

        var colunas = new Dictionary<string, string>();
        await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await leitor.ReadAsync(TestContext.Current.CancellationToken))
        {
            colunas.Add(leitor.GetString(0), leitor.GetString(1));
        }

        return colunas;
    }

    private async Task<List<string>> ListarSchemasDaTabelaAsync(string tabela)
    {
        await using var comando = postgres.DataSource.CreateCommand(
            "select table_schema from information_schema.tables where table_name = $1");
        comando.Parameters.Add(new NpgsqlParameter { Value = tabela });

        var schemas = new List<string>();
        await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await leitor.ReadAsync(TestContext.Current.CancellationToken))
        {
            schemas.Add(leitor.GetString(0));
        }

        return schemas;
    }

    private async Task<List<string>> ListarMigrationsAplicadasAsync(string schema)
    {
        await using var comando = postgres.DataSource.CreateCommand(
            $"select migration_id from {schema}.{DatabaseServiceCollectionExtensions.MigrationsHistoryTableName} order by migration_id");

        var migrations = new List<string>();
        await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await leitor.ReadAsync(TestContext.Current.CancellationToken))
        {
            migrations.Add(leitor.GetString(0));
        }

        return migrations;
    }

    private static DateTime TruncarParaMicrossegundos(DateTime valor)
    {
        return new DateTime(valor.Ticks - (valor.Ticks % 10), valor.Kind);
    }
}
