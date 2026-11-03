using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace Actum.Shared.Infrastructure.Persistence;

/// <summary>
/// Migrations history repository with snake_case column names, so the history table can be
/// queried without quotes. EFCore.NamingConventions does not rename these columns.
/// </summary>
/// <param name="dependencies">History repository dependencies</param>
#pragma warning disable EF1001 // NpgsqlHistoryRepository é API interna do provider; não há ponto de extensão público equivalente.
internal sealed class SnakeCaseHistoryRepository(HistoryRepositoryDependencies dependencies)
    : NpgsqlHistoryRepository(dependencies)
{
    protected override void ConfigureTable(EntityTypeBuilder<HistoryRow> history)
    {
        base.ConfigureTable(history);

        history.Property(h => h.MigrationId).HasColumnName("migration_id");
        history.Property(h => h.ProductVersion).HasColumnName("product_version");
    }
}
#pragma warning restore EF1001
