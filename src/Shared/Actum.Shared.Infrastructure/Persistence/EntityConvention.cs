using Actum.Shared.Abstractions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Actum.Shared.Infrastructure.Persistence;

/// <summary>
/// Model convention that configures the <see cref="Entity"/> fields of every root entity type derived
/// from it: identity key, unique identifier and database defaults.
/// </summary>
/// <remarks>
/// The database defaults serve inserts made outside the application. The application always sends the
/// values (<see cref="ValueGenerated.Never"/>), otherwise EF Core would treat <see langword="false"/> in
/// <see cref="Entity.IsActive"/> as "not informed" and let the database apply <see langword="true"/>.
/// Runs on model finalizing, so it sees entities registered after <c>base.OnModelCreating</c>, and
/// explicit module configuration takes precedence over it.
/// </remarks>
public sealed class EntityConvention : IModelFinalizingConvention
{
    /// <summary>
    /// Database default of <see cref="Entity.Identifier"/> (PostgreSQL 18 or later)
    /// </summary>
    public const string IdentifierDefaultSql = "uuidv7()";

    /// <summary>
    /// Database default of <see cref="Entity.CreateIn"/> and <see cref="Entity.UpdateIn"/>
    /// (current local date and time, <c>timestamp without time zone</c>)
    /// </summary>
    public const string DateTimeDefaultSql = "localtimestamp";

    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        var entityTypes = modelBuilder.Metadata.GetEntityTypes()
            .Where(entityType => entityType.BaseType is null && typeof(Entity).IsAssignableFrom(entityType.ClrType))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            var builder = entityType.Builder;

            builder.PrimaryKey([nameof(Entity.Id)]);

            AlwaysSendValue(builder.Property(typeof(Guid), nameof(Entity.Identifier))?.HasDefaultValueSql(IdentifierDefaultSql));
            AlwaysSendValue(builder.Property(typeof(DateTime), nameof(Entity.CreateIn))?.HasDefaultValueSql(DateTimeDefaultSql));
            AlwaysSendValue(builder.Property(typeof(DateTime), nameof(Entity.UpdateIn))?.HasDefaultValueSql(DateTimeDefaultSql));
            AlwaysSendValue(builder.Property(typeof(bool), nameof(Entity.IsActive))?.HasDefaultValue(true));

            builder.HasIndex([nameof(Entity.Identifier)])?.IsUnique(true);
        }
    }

    private static void AlwaysSendValue(IConventionPropertyBuilder? property)
    {
        property?.ValueGenerated(ValueGenerated.Never, fromDataAnnotation: true);
    }
}
