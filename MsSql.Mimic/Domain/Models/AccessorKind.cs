namespace MsSql.Mimic.Domain.Models;

/// <summary>
/// Specifies the accessor pattern to use when generating a C# property.
/// </summary>
public enum AccessorKind
{
    /// <summary>
    /// Generates a property with both get and set accessors.
    /// </summary>
    GetSet,

    /// <summary>
    /// Generates a property with get and init accessors.
    /// </summary>
    GetInit
}