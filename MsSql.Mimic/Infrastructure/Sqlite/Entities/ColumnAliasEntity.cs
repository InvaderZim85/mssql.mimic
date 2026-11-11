namespace MsSql.Mimic.Infrastructure.Sqlite.Entities;

public sealed class ColumnAliasEntity
{
    public int Id { get; set; }
    public int TableAliasId { get; set; }
    public TableAliasEntity? TableAlias { get; set; }
    public required string ColumnName { get; set; }
    public required string PropertyName { get; set; }
    public required string AccessorKind { get; set; }
}