namespace MsSql.Mimic.Domain.Models;

public sealed class TableDefinition
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<ColumnDefinition> Columns { get; init; }
}