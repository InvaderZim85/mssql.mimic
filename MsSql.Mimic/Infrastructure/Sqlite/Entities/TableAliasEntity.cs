using System.ComponentModel.DataAnnotations;

namespace MsSql.Mimic.Infrastructure.Sqlite.Entities;

public sealed class TableAliasEntity
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(128)]
    public required string Server { get; set; }
    
    [Required]
    [MaxLength(128)]
    public required string Database { get; set; }
    
    [MaxLength(32)]
    public required string Schema { get; set; }
    
    [Required]
    [MaxLength(128)]
    public required string TableName { get; set; }
    
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