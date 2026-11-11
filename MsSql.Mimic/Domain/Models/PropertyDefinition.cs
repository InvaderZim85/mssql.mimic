namespace MsSql.Mimic.Domain.Models;

public sealed class PropertyDefinition
{
    public required string ColumnName { get; init; }
    public required string PropertyName { get; init; }
    public required string CSharpTypeName { get; init; }
    public required bool IsNullable { get; init; }
    public required bool IsPrimaryKey { get; init; }
    public required int? MaxLength { get; init; }
    public required AccessorKind AccessorKind { get; init; }
}