using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Domain.Services;
using MsSql.Mimic.Infrastructure.Sqlite.Entities;

namespace MsSql.Mimic.Application.Services;

public sealed class PropertyDefinitionFactory(CSharpNameService nameService, SqlTypeMapper typeMapper)
{
    public IReadOnlyList<PropertyDefinition> CreateProperties(
        TableDefinition tableDefinition,
        TableAliasEntity? aliasEntity)
    {
        return
        [
            .. tableDefinition.Columns
                .Select(column =>
                {
                    var columnAlias = aliasEntity?.Columns
                        .SingleOrDefault(aliasColumn => aliasColumn.ColumnName == column.Name);

                    var propertyName = columnAlias?.PropertyName ?? nameService.ToPropertyName(column.Name);

                    var accessorKind = Enum.TryParse<AccessorKind>(columnAlias?.AccessorKind, out var parsedAccessorKind)
                        ? parsedAccessorKind
                        : AccessorKind.GetSet;

                    return new PropertyDefinition
                    {
                        ColumnName = column.Name,
                        PropertyName = propertyName,
                        CSharpTypeName = typeMapper.MapToCSharpType(column.SqlDataType, column.IsNullable),
                        IsNullable = column.IsNullable,
                        IsPrimaryKey = column.IsPrimaryKey,
                        MaxLength = column.MaxLength,
                        AccessorKind = accessorKind
                    };
                })
        ];
    }
}