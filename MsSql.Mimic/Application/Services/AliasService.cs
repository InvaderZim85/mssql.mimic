using Microsoft.EntityFrameworkCore;

using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Infrastructure.Sqlite;
using MsSql.Mimic.Infrastructure.Sqlite.Entities;

namespace MsSql.Mimic.Application.Services;

/// <summary>
/// Provides persistence and retrieval of alias metadata for database tables, including generated class names, column mappings, and related configuration.
/// </summary>
/// <param name="context">
/// The alias database context used to query and store table alias information.
/// </param>
public class AliasService(AliasDbContext context)
{
    /// <summary>
    /// Loads the stored alias information for the specified table in the database identified by the connection profile, including its column alias entries.
    /// </summary>
    /// <param name="profile">The connection profile identifying the server and database that contain the table.</param>
    /// <param name="table">The table whose alias information is loaded.</param>
    /// <param name="ct">The cancellation token used to cancel the asynchronous load operation.</param>
    /// <returns>A task that represents the asynchronous load operation and contains the matching table alias entity, or null if no alias information exists.</returns>
    internal async Task<TableAliasEntity?> LoadTableAliasAsync(
        ConnectionProfile profile,
        TableEntry table,
        CancellationToken ct = default)
    {
        return await context.TableAliases
            .Include(entity => entity.Columns)
            .SingleOrDefaultAsync(entity => entity.Server == profile.Server &&
                                            entity.Database == profile.Database &&
                                            entity.Schema == table.Schema &&
                                            entity.TableName == table.Name, ct);
    }

    /// <summary>
    /// Creates or updates the stored alias information for the specified table, synchronizing its class alias, Entity Framework Core attribute preference, and column aliases with the supplied property definitions.
    /// </summary>
    /// <param name="profile">The connection profile identifying the server and database that contain the table.</param>
    /// <param name="table">The table whose alias information is saved.</param>
    /// <param name="className">The class name alias to store for the table.</param>
    /// <param name="useEfCoreAttributes">A value indicating whether Entity Framework Core attributes should be used for the table.</param>
    /// <param name="properties">The property definitions used to create, update, and remove persisted column alias entries.</param>
    /// <param name="ct">The cancellation token used to cancel the asynchronous save operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    internal async Task SaveAsync(
        ConnectionProfile profile,
        TableEntry table,
        string className,
        bool useEfCoreAttributes,
        IReadOnlyList<PropertyDefinition> properties,
        CancellationToken ct = default)
    {
        var entity = await context.TableAliases
            .Include(entity => entity.Columns)
            .SingleOrDefaultAsync(entity => entity.Server == profile.Server &&
                                            entity.Database == profile.Database &&
                                            entity.TableName == table.Name, ct);

        if (entity is null)
        {
            entity = new TableAliasEntity
            {
                Server = profile.Server,
                Database = profile.Database,
                Schema = table.Schema,
                TableName = table.Name,
                ClassName = className,
                UseEfCoreAttributes = useEfCoreAttributes
            };

            context.TableAliases.Add(entity);
        }
        else
        {
            entity.ClassName = className;
            entity.UseEfCoreAttributes = useEfCoreAttributes;
        }

        foreach (var property in properties)
        {
            var columnEntity = entity.Columns
                .SingleOrDefault(column => column.ColumnName == property.ColumnName);

            if (columnEntity is null)
            {
                columnEntity = new ColumnAliasEntity
                {
                    ColumnName = property.ColumnName,
                    PropertyName = property.PropertyName
                };

                entity.Columns.Add(columnEntity);
            }
            else
            {
                columnEntity.PropertyName = property.PropertyName;
            }
        }

        RemoveDeletedColumns(entity, properties);

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Removes column aliases from the specified table alias entity when their column names are not present in the supplied property definitions.
    /// </summary>
    /// <param name="entity">The table alias entity whose column aliases are checked and updated.</param>
    /// <param name="properties">The current property definitions used to determine which column aliases should be retained.</param>
    private static void RemoveDeletedColumns(
        TableAliasEntity entity,
        IReadOnlyList<PropertyDefinition> properties)
    {
        var currentColumnNames = properties
            .Select(column => column.ColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var deletedColumns = entity.Columns
            .Where(column => !currentColumnNames.Contains(column.ColumnName))
            .ToList();

        foreach (var deletedColumn in deletedColumns)
            entity.Columns.Remove(deletedColumn);
    }
}