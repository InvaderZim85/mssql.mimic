using Microsoft.EntityFrameworkCore;

using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Infrastructure.Sqlite;
using MsSql.Mimic.Infrastructure.Sqlite.Entities;

namespace MsSql.Mimic.Application.Services;

public class AliasService(AliasDbContext context)
{
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
                    PropertyName = property.PropertyName,
                    AccessorKind = property.AccessorKind.ToString()
                };

                entity.Columns.Add(columnEntity);
            }
            else
            {
                columnEntity.PropertyName = property.PropertyName;
                columnEntity.AccessorKind = property.AccessorKind.ToString();
            }
        }

        RemoveDeletedColumns(entity, properties);

        await context.SaveChangesAsync(ct);
    }

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