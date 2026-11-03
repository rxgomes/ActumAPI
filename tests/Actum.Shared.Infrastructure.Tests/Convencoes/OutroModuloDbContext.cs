using Actum.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Actum.Shared.Infrastructure.Tests.Convencoes;

/// <summary>
/// Segundo módulo fictício, para verificar o isolamento de schema e de histórico de migrations.
/// </summary>
public sealed class OutroModuloDbContext(DbContextOptions<OutroModuloDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "teste_convencoes_b";

    protected override string Schema => SchemaName;

    public static OutroModuloDbContext Criar(NpgsqlDataSource dataSource)
    {
        var options = new DbContextOptionsBuilder<OutroModuloDbContext>();
        DatabaseServiceCollectionExtensions.ConfigureModuleDbContext(options, dataSource, SchemaName);
        return new OutroModuloDbContext(options.Options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ClienteCadastro>();
    }
}

/// <summary>
/// Usada apenas pelo <c>dotnet ef</c> para gerar as migrations de teste (não abre conexão).
/// </summary>
public sealed class OutroModuloDbContextFactory : IDesignTimeDbContextFactory<OutroModuloDbContext>
{
    public OutroModuloDbContext CreateDbContext(string[] args)
    {
        return OutroModuloDbContext.Criar(NpgsqlDataSource.Create("Host=localhost;Database=design_time"));
    }
}
