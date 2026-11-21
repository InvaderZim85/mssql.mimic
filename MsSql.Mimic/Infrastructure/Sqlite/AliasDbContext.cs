using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MsSql.Mimic.Infrastructure.Sqlite.Entities;

using System.Linq.Expressions;

namespace MsSql.Mimic.Infrastructure.Sqlite;

/// <summary>
/// Represents the Entity Framework Core database context used to persist table and column alias metadata.
/// </summary>
/// <param name="options">
/// The options used to configure the context, including the database provider and connection settings.
/// </param>
/// <remarks>
/// Manages alias records for database tables and columns, including table-to-class mappings,
/// column-to-property mappings, accessor metadata, uniqueness constraints, required string lengths,
/// and cascade deletion of column aliases when their associated table alias is removed.
/// </remarks>
public sealed class AliasDbContext(DbContextOptions<AliasDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets or sets the connections.
    /// </summary>
    internal DbSet<ConnectionEntity> Connections => Set<ConnectionEntity>();
    
    /// <summary>
    /// Gets or sets the table aliases.
    /// </summary>
    internal DbSet<TableAliasEntity> TableAliases => Set<TableAliasEntity>();

    /// <summary>
    /// Gets or sets the column aliases.
    /// </summary>
    internal DbSet<ColumnAliasEntity> ColumnAliases => Set<ColumnAliasEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConnectionEntity>(entity =>
        {
            entity.ToTable("ConnectionHistory");

            entity.HasKey(key => key.Id);

            SetPropertyRequiredLength(entity, property => property.Server, 256);

            SetPropertyRequiredLength(entity, property => property.Database, 128);

            entity.Property(property => property.UserName).HasMaxLength(256);

            entity.Property(property => property.ProtectedPassword).HasMaxLength(4096);

            SetPropertyRequiredLength(entity, property => property.ApplicationIntent, 32);
            
            entity.Property(property => property.LastUsedAtUtc).IsRequired();

            entity.HasIndex(index => new
            {
                index.Server,
                index.Database,
                index.UseIntegratedSecurity,
                index.UserName
            });
        });
        
        modelBuilder.Entity<TableAliasEntity>(entity =>
        {
            entity.ToTable("TableAliases");

            entity.HasKey(key => key.Id);

            entity.HasIndex(index => new
            {
                index.Database,
                index.TableName
            }).IsUnique();

            entity.HasMany(table => table.Columns)
                .WithOne(column => column.TableAlias)
                .HasForeignKey(column => column.TableAliasId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ColumnAliasEntity>(entity =>
        {
            entity.ToTable("ColumnAliases");

            entity.HasKey(key => key.Id);

            entity.HasIndex(index => new
            {
                index.TableAliasId,
                index.ColumnName
            }).IsUnique();
        });

        return;

        void SetPropertyRequiredLength<TEntity, TProperty>(EntityTypeBuilder<TEntity> entity, Expression<Func<TEntity, TProperty>> propertyExpression, int maxLength) where TEntity : class
        {
            entity.Property(propertyExpression)
                .IsRequired()
                .HasMaxLength(maxLength);
        }
    }
}