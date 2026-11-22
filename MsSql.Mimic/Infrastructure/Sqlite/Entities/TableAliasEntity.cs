using System.ComponentModel.DataAnnotations;

namespace MsSql.Mimic.Infrastructure.Sqlite.Entities;

/// <summary>
/// Represents a persisted table alias definition used to map a SQL Server table to an application class name.
/// </summary>
/// <remarks>
/// Stores the identifying table context, alias metadata, and the associated column alias definitions for a table.
/// This entity is intended for use with the alias database context and is mapped to the table alias storage model.
/// </remarks>
public sealed class TableAliasEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for the table alias entity.
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the SQL Server instance name associated with the table alias.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public required string Server { get; set; }

    /// <summary>
    /// Gets or sets the database name associated with the table alias.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public required string Database { get; set; }

    /// <summary>
    /// Gets or sets the database schema that contains the aliased table.
    /// </summary>
    [MaxLength(32)]
    public required string Schema { get; set; }

    /// <summary>
    /// Gets or sets the name of the database table associated with this alias record.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public required string TableName { get; set; }

    /// <summary>
    /// Gets or sets the C# class name to use for the generated table model.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public required string ClassName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Entity Framework Core attributes should be applied when generating the table model.
    /// </summary>
    public bool UseEfCoreAttributes { get; set; }

    /// <summary>
    /// Gets or sets the column aliases associated with this table alias.
    /// </summary>
    public List<ColumnAliasEntity> Columns { get; set; } = [];
}