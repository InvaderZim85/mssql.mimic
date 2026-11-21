using System.ComponentModel.DataAnnotations;

namespace MsSql.Mimic.Infrastructure.Sqlite.Entities;

/// <summary>
/// Represents a persisted column alias for a table alias.
/// </summary>
/// <remarks>
/// Stores the mapping between a database column name and the corresponding property name used by the application.
/// Instances are associated with a table alias and are intended for persistence through the alias database context.
/// </remarks>
public sealed class ColumnAliasEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this column alias.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the table alias associated with this column alias.
    /// </summary>
    public int TableAliasId { get; set; }

    /// <summary>
    /// Gets or sets the table alias associated with this column alias.
    /// </summary>
    public TableAliasEntity? TableAlias { get; set; }

    /// <summary>
    /// Gets or sets the database column name associated with the column alias.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public required string ColumnName { get; set; }

    /// <summary>
    /// Gets or sets the C# property name mapped to the database column alias.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public required string PropertyName { get; set; }
}