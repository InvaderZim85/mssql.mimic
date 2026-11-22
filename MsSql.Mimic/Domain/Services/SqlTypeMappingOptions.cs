namespace MsSql.Mimic.Domain.Services;

/// <summary>
/// Provides configuration options for mapping SQL type names to their corresponding target type representations.
/// </summary>
/// <param name="Mappings">
/// A read-only collection of SQL type mapping entries, where each key represents a source SQL type name and each value represents the mapped target type name.
/// </param>
public sealed record SqlTypeMappingOptions(IReadOnlyDictionary<string, string> Mappings);