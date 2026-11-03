using Actum.Modules.Tarefas.Domain;
using Actum.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Actum.Modules.Tarefas.Persistence;

/// <summary>
/// DbContext of the Tarefas module (schema <c>tarefas</c>)
/// </summary>
/// <remarks>
/// No <c>DbSet</c> properties: EF Core would name the tables after them (plural). Use <c>Set&lt;T&gt;()</c>, so the
/// tables get the entity names (<c>tarefa</c>, <c>empresa</c>...).
/// </remarks>
/// <param name="options">Context options</param>
public sealed class TarefasDbContext(DbContextOptions<TarefasDbContext> options) : ModuleDbContext(options)
{
    /// <summary>
    /// Database schema of the module
    /// </summary>
    public const string SchemaName = "tarefas";

    /// <summary>
    /// PostgreSQL extension used by the text search (accent-insensitive)
    /// </summary>
    public const string UnaccentExtension = "unaccent";

    /// <inheritdoc />
    protected override string Schema => SchemaName;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Sem schema: a função unaccent() fica no schema padrão (public) e é chamada sem qualificação.
        modelBuilder.HasPostgresExtension(UnaccentExtension);
    }

    /// <summary>
    /// Create a context over a data source, with the module options (used by tests and by the design-time factory)
    /// </summary>
    /// <param name="dataSource">Data source of the Actum database</param>
    /// <returns>New context</returns>
    public static TarefasDbContext Criar(NpgsqlDataSource dataSource)
    {
        var options = new DbContextOptionsBuilder<TarefasDbContext>();
        DatabaseServiceCollectionExtensions.ConfigureModuleDbContext(options, dataSource, SchemaName);
        return new TarefasDbContext(options.Options);
    }
}

/// <summary>
/// Used only by <c>dotnet ef</c>. Connects to <c>ConnectionStrings__Actum</c> (environment variable) or, when not
/// set, to the local development database (<c>actum</c>/<c>actum</c>).
/// </summary>
public sealed class TarefasDbContextFactory : IDesignTimeDbContextFactory<TarefasDbContext>
{
    private const string ConexaoDeDesenvolvimento = "Host=localhost;Port=5432;Database=actum;Username=actum;Password=actum";

    /// <inheritdoc />
    public TarefasDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("ConnectionStrings__Actum") ?? ConexaoDeDesenvolvimento;

        return TarefasDbContext.Criar(NpgsqlDataSource.Create(conexao));
    }
}
