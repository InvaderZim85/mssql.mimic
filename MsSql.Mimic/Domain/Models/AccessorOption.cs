namespace MsSql.Mimic.Domain.Models;

/// <summary>
/// Represents an available accessor configuration for generated C# properties.
/// </summary>
/// <param name="Kind">
/// The accessor pattern represented by the option.
/// </param>
/// <param name="DisplayName">
/// The display text shown for the option.
/// </param>
public sealed record AccessorOption(
    AccessorKind Kind,
    string DisplayName)
{
    /// <summary>
    /// Gets the available accessor options for generated C# properties.
    /// </summary>
    /// <remarks>
    /// Includes supported accessor patterns such as get/set and get/init.
    /// </remarks>
    public static IReadOnlyList<AccessorOption> All { get; } =
    [
        new(AccessorKind.GetSet, "'Get' / 'Set'"),
        new(AccessorKind.GetInit, "'Get' / 'Init'")
    ];
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return DisplayName;
    }
}