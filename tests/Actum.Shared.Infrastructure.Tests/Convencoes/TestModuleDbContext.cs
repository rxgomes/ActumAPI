using Actum.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Actum.Shared.Infrastructure.Tests.Convencoes;

/// <summary>
/// Módulo fictício usado para verificar as convenções de banco do <see cref="ModuleDbContext"/>.
/// </summary>
public sealed class TestModuleDbContext(DbContextOptions<TestModuleDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "teste_convencoes";

    protected override string Schema => SchemaName;

    public static TestModuleDbContext Criar(NpgsqlDataSource dataSource)
    {
        var options = new DbContextOptionsBuilder<TestModuleDbContext>();
        DatabaseServiceCollectionExtensions.ConfigureModuleDbContext(options, dataSource, SchemaName);
        return new TestModuleDbContext(options.Options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PedidoItem>();
        modelBuilder.Entity<ProdutoTeste>();
    }
}

/// <summary>
/// Usada apenas pelo <c>dotnet ef</c> para gerar as migrations de teste (não abre conexão).
/// </summary>
public sealed class TestModuleDbContextFactory : IDesignTimeDbContextFactory<TestModuleDbContext>
{
    public TestModuleDbContext CreateDbContext(string[] args)
    {
        return TestModuleDbContext.Criar(NpgsqlDataSource.Create("Host=localhost;Database=design_time"));
    }
}
