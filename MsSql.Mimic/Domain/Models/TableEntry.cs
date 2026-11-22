namespace MsSql.Mimic.Domain.Models;

/// <summary>
/// Represents a database table identified by its schema and table name.
/// </summary>
/// <remarks>
/// Used as a lightweight model for selecting, loading, and referencing SQL Server table metadata.
/// </remarks>
public sealed class TableEntry
{
    /// <summary>
    /// Gets the SQL Server schema that contains the table.
    /// </summary>
    public required string Schema { get; init; }

    /// <summary>
    /// Gets the table name.
    /// </summary>
    public required string Name { get; init; }
}