using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Actum.Shared.Infrastructure.Persistence;

/// <summary>
/// Registration of the Actum database and of module DbContexts
/// </summary>
public static class DatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Name of the connection string shared by every module
    /// </summary>
    public const string ConnectionStringName = "Actum";

    /// <summary>
    /// Name of the migrations history table created in each module schema
    /// </summary>
    public const string MigrationsHistoryTableName = "__ef_migrations_history";

    /// <summary>
    /// Register the single <see cref="NpgsqlDataSource"/> (connection pool) used by every module,
    /// built from the <c>ConnectionStrings:Actum</c> configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration containing the connection string</param>
    /// <returns>The same service collection</returns>
    /// <exception cref="InvalidOperationException">The connection string is not configured</exception>
    public static IServiceCollection AddActumDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' não configurada (ConnectionStrings:{ConnectionStringName}).");
        }

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        return services;
    }

    /// <summary>
    /// Register a module DbContext using the shared data source, snake_case naming and a migrations
    /// history table inside the module schema. Requires <see cref="AddActumDatabase"/>.
    /// </summary>
    /// <typeparam name="TContext">Module DbContext</typeparam>
    /// <param name="services">Service collection</param>
    /// <param name="schema">Module schema, the same returned by the context <c>Schema</c> property</param>
    /// <returns>The same service collection</returns>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : ModuleDbContext
    {
        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            ConfigureModuleDbContext(options, serviceProvider.GetRequiredService<NpgsqlDataSource>(), schema);
        });

        return services;
    }

    /// <summary>
    /// Apply the module DbContext options (snake_case naming, history table in the module schema).
    /// Used by <see cref="AddModuleDbContext{TContext}"/> and by design-time factories and tests.
    /// </summary>
    /// <param name="options">Options builder</param>
    /// <param name="dataSource">Data source of the Actum database</param>
    /// <param name="schema">Module schema</param>
    /// <returns>The same options builder</returns>
    public static DbContextOptionsBuilder ConfigureModuleDbContext(
        DbContextOptionsBuilder options,
        NpgsqlDataSource dataSource,
        string schema)
    {
        options
            .UseNpgsql(dataSource, npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTableName, schema))
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IHistoryRepository, SnakeCaseHistoryRepository>();

        return options;
    }
}
