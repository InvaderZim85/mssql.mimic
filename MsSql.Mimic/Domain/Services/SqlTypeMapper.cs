namespace MsSql.Mimic.Domain.Services;

/// <summary>
/// Provides mapping from SQL Server data type names to C# type names.
/// </summary>
/// <param name="options">
/// The mapping options that define the SQL Server type names and their corresponding C# type names.
/// </param>
/// <remarks>
/// SQL Server type names are resolved using the configured mappings. Type name lookup is performed using a normalized lowercase representation of the supplied SQL type name. Types that are not present in the configured mappings are mapped to <c>object</c>.
/// </remarks>
public sealed class SqlTypeMapper(SqlTypeMappingOptions options)
{
    /// <summary>
    /// Maps a SQL Server data type name to its corresponding C# type name.
    /// </summary>
    /// <param name="sqlType">The SQL Server data type name to map.</param>
    /// <param name="isNullable">A value indicating whether the resulting C# type name should be marked as nullable.</param>
    /// <returns>The C# type name that corresponds to the specified SQL Server data type, including a nullable suffix when requested.</returns>
    public string MapToCSharpType(
        string sqlType,
        bool isNullable)
    {
        var normalizedSqlType = sqlType.ToLowerInvariant();

        var typeName = options.Mappings.TryGetValue(normalizedSqlType, out var mappedType)
            ? mappedType
            : "object";

        return isNullable ? $"{typeName}?" : typeName;
    }
}