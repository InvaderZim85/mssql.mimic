namespace MsSql.Mimic.Domain.Models;

/// <summary>
/// Represents the schema metadata for a column in a Microsoft SQL Server table.
/// </summary>
/// <remarks>
/// Contains the column name, SQL data type, nullability, primary key status, and optional size or numeric characteristics such as maximum length, precision, and scale.
/// </remarks>
public sealed class ColumnDefinition
{
    /// <summary>
    /// Gets the column name as defined in the SQL Server table schema.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the SQL Server data type declared for the column.
    /// </summary>
    public required string SqlDataType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the column allows null values.
    /// </summary>
    public required bool IsNullable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the column is part of the table's primary key.
    /// </summary>
    public required bool IsPrimaryKey { get; init; }

    /// <summary>
    /// Gets the maximum length defined for the column, when applicable to the SQL data type.
    /// </summary>
    public required int? MaxLength { get; init; }

    /// <summary>
    /// Gets the numeric precision defined for the column, or null when precision is not applicable.
    /// </summary>
    public required int? Precision { get; init; }

    /// <summary>
    /// Gets the number of digits allowed to the right of the decimal point for numeric column types, or null when scale is not applicable.
    /// </summary>
    public required int? Scale { get; init; }
}