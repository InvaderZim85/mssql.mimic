namespace MsSql.Mimic.Domain.Models;

/// <summary>
/// Represents the metadata definition of a SQL Server table.
/// </summary>
/// <remarks>
/// Contains the table schema name, table name, and ordered column definitions used by schema-reading and code-generation services.
/// </remarks>
public sealed class TableDefinition
{
    /// <summary>
    /// Gets the database schema that contains the table.
    /// </summary>
    public required string Schema { get; init; }

    /// <summary>
    /// Gets the table name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the collection of column definitions that describe the table schema.
    /// </summary>
    public required IReadOnlyList<ColumnDefinition> Columns { get; init; }
}