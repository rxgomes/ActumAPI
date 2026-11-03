using Actum.Shared.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Actum.Shared.Infrastructure.Persistence;

/// <summary>
/// Base DbContext for business modules. Places every table in the module schema and maps
/// <see cref="DateTime"/> to <c>timestamp without time zone</c> by convention.
/// </summary>
/// <remarks>
/// <para>
/// Register the concrete context with
/// <see cref="DatabaseServiceCollectionExtensions.AddModuleDbContext{TContext}"/>, using the same
/// schema returned by <see cref="Schema"/>.
/// </para>
/// <para>
/// Columns <c>timestamp without time zone</c> do not accept <see cref="DateTime"/> values with
/// <see cref="DateTimeKind.Utc"/>: Npgsql throws on write. Use <see cref="DateTimeKind.Unspecified"/>
/// or <see cref="DateTimeKind.Local"/> (for example <see cref="DateTime.Now"/>).
/// </para>
/// <para>
/// Entities derived from <see cref="Entity"/> are configured by <see cref="EntityConvention"/>. On save,
/// inserted entities get <see cref="Entity.CreateIn"/> and <see cref="Entity.UpdateIn"/> with the current
/// local date and time; changed entities get <see cref="Entity.UpdateIn"/> and keep the original
/// <see cref="Entity.CreateIn"/>.
/// </para>
/// </remarks>
/// <param name="options">Context options</param>
public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>
    /// Database schema owned by the module, in lowercase snake_case (for example <c>vendas</c>)
    /// </summary>
    protected abstract string Schema { get; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("timestamp without time zone");
        configurationBuilder.Conventions.Add(_ => new EntityConvention());

        base.ConfigureConventions(configurationBuilder);
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        FillEntityDates();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        FillEntityDates();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void FillEntityDates()
    {
        ChangeTracker.DetectChanges();

        var now = DateTime.Now;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(entity => entity.CreateIn).CurrentValue = now;
                entry.Property(entity => entity.UpdateIn).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                var createIn = entry.Property(entity => entity.CreateIn);
                createIn.CurrentValue = createIn.OriginalValue;
                createIn.IsModified = false;

                if (entry.Properties.Any(property => property.IsModified))
                {
                    entry.Property(entity => entity.UpdateIn).CurrentValue = now;
                }
            }
        }
    }
}
