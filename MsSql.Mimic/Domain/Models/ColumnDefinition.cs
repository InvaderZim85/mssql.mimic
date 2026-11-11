namespace MsSql.Mimic.Domain.Models;

public sealed class ColumnDefinition
{
    public required string Name { get; init; }
    public required string SqlDataType { get; init; }
    public required bool IsNullable { get; init; }
    public required bool IsPrimaryKey { get; init; }
    public required int? MaxLength { get; init; }
    public required int? Precision { get; init; }
    public required int? Scale { get; init; }
}