using MsSql.Mimic.Domain.Models;

namespace MsSql.Mimic.Application.Options;

/// <summary>
/// Represents the configuration used to generate a C# class from a SQL Server table definition.
/// </summary>
/// <remarks>
/// Provides the metadata and generation preferences required by the class generator, including naming,
/// schema and table context, selected property definitions, accessor style, and optional output features
/// such as Entity Framework Core attributes and generated summaries.
/// </remarks>
public sealed class ClassGenerationOptions
{
    /// <summary>
    /// Gets the desired namespace.
    /// </summary>
    public required string NamespaceName { get; init; }
    
    /// <summary>
    /// Gets the schame of the selected table.
    /// </summary>
    public required string SchemaName { get; init; }
    
    /// <summary>
    /// Gets the desired class name.
    /// </summary>
    public required string ClassName { get; init; }
    
    /// <summary>
    /// Gets the desired table name.
    /// </summary>
    public required string TableName { get; init; }
    
    /// <summary>
    /// Gets the value that indicates whether EF Core attributes should be added.
    /// </summary>
    public required bool UseEfCoreAttributes { get; init; }
    
    /// <summary>
    /// Gets the value that indicates whether a summary should be created.
    /// </summary>
    public required bool GenerateSummary { get; init; }
    
    /// <summary>
    /// Gets the desired accessor kind.
    /// </summary>
    public required AccessorKind AccessorKind { get; init; }
    
    /// <summary>
    /// Gets the list with the properties that should be created.
    /// </summary>
    /// <remarks>
    /// Each property represents a column of the table.
    /// </remarks>
    public required IReadOnlyList<PropertyDefinition> Properties { get; init; }
}