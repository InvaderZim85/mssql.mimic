namespace MsSql.Mimic.Domain.Models;

/// <summary>
/// Represents a prepared database table model used for C# class generation.
/// </summary>
/// <remarks>
/// Contains the resolved table metadata, generated class name, Entity Framework Core attribute preference,
/// and generated property definitions for a selected SQL Server table.
/// </remarks>
public sealed class PreparedTableDefinition
{
    /// <summary>
    /// Gets the source database table associated with the prepared table definition.
    /// </summary>
    /// <remarks>
    /// Identifies the table whose schema and alias information are used to produce the generated class name, Entity Framework Core attribute setting, and property definitions.
    /// </remarks>
    public required TableEntry Table { get; init; }

    /// <summary>
    /// Gets the generated C# class name for the prepared table definition.
    /// </summary>
    /// <remarks>
    /// Uses the configured table alias class name when available; otherwise, it is derived from the source table name.
    /// </remarks>
    public required string ClassName { get; init; }

    /// <summary>
    /// Gets a value indicating whether generated class output should include Entity Framework Core attributes.
    /// </summary>
    /// <remarks>
    /// Reflects the table alias setting when one is available; otherwise, defaults to enabling Entity Framework Core attributes.
    /// </remarks>
    public required bool UseEfCoreAttributes { get; init; }

    /// <summary>
    /// Gets the prepared property definitions generated for the table columns.
    /// </summary>
    /// <remarks>
    /// Contains the column-to-property mapping details used to populate the UI and generate the corresponding C# class members.
    /// </remarks>
    public required IReadOnlyList<PropertyDefinition> Properties { get; init; }
}