namespace MsSql.Mimic.Domain.Models;

/// <summary>
/// Describes the generated C# property metadata for a database column, including its source column name,
/// target property name, C# type, nullability, primary key status, and length constraint information.
/// </summary>
public sealed class PropertyDefinition
{
    /// <summary>
    /// Gets the database column name mapped to the generated property.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// Gets the generated C# property name used for the mapped database column.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets the C# type name assigned to the generated property.
    /// </summary>
    public required string CSharpTypeName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the database column allows null values.
    /// </summary>
    public required bool IsNullable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the database column is part of the primary key.
    /// </summary>
    public required bool IsPrimaryKey { get; init; }

    /// <summary>
    /// Gets the maximum length defined for the property, or null when no maximum length is specified.
    /// </summary>
    public required int? MaxLength { get; init; }
    
    /// <summary>
    /// Gets the numeric precision defined for the property, or null when precision is not applicable.
    /// </summary>
    public required int? Precision { get; init; }

    /// <summary>
    /// Gets the number of digits allowed to the right of the decimal point, or null when scale is not applicable.
    /// </summary>
    public required int? Scale { get; init; }

    /// <summary>
    /// Gets a value indicating whether the property maps to a SQL Server rowversion or timestamp column.
    /// </summary>
    public required bool IsRowVersion { get; init; }
}