using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Domain.Services;
using MsSql.Mimic.Infrastructure.Sqlite.Entities;

namespace MsSql.Mimic.Application.Services;

/// <summary>
/// Creates property definitions from table metadata by converting database column information into C#-oriented property descriptors.
/// </summary>
/// <param name="nameService">The service used to generate valid C# property names from database identifiers.</param>
/// <param name="typeMapper">The service used to map SQL column types to corresponding C# types.</param>
public sealed class PropertyDefinitionFactory(CSharpNameService nameService, SqlTypeMapper typeMapper)
{
    /// <summary>
    /// Creates property definitions for the columns in the specified table definition, applying any matching column aliases when provided.
    /// </summary>
    /// <param name="tableDefinition">The table definition containing the columns to convert into property definitions.</param>
    /// <param name="aliasEntity">The optional table alias entity used to override generated property names.</param>
    /// <returns>A read-only list of property definitions created from the table columns.</returns>
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

                    return new PropertyDefinition
                    {
                        ColumnName = column.Name,
                        PropertyName = propertyName,
                        CSharpTypeName = typeMapper.MapToCSharpType(column.SqlDataType, column.IsNullable),
                        IsNullable = column.IsNullable,
                        IsPrimaryKey = column.IsPrimaryKey,
                        MaxLength = column.MaxLength,
                        Precision = column.Precision,
                        Scale = column.Scale,
                        IsRowVersion = column.SqlDataType.Equals("rowversion", StringComparison.OrdinalIgnoreCase) ||
                                       column.SqlDataType.Equals("timestamp", StringComparison.OrdinalIgnoreCase)
                    };
                })
        ];
    }
}