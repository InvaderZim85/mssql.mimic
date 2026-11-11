namespace MsSql.Mimic.Domain.Models;

public sealed class TableEntry
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
}