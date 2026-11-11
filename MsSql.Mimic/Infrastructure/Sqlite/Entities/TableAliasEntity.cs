namespace MsSql.Mimic.Infrastructure.Sqlite.Entities;

public sealed class TableAliasEntity
{
    public int Id { get; set; }
    
    public required string Server { get; set; }
    
    public required string Database { get; set; }
    
    public required string Schema { get; set; }
    
    public required string TableName { get; set; }
    
    public required string ClassName { get; set; }
    
    public bool UseEfCoreAttributes { get; set; }
    
    public List<ColumnAliasEntity> Columns { get; set; } = [];
}