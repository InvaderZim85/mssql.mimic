namespace MsSql.Mimic.Domain.Dtos;

/// <summary>
/// Represents a data transfer object that associates a numeric identifier with a string value.
/// </summary>
/// <param name="Id">The numeric identifier.</param>
/// <param name="Value">The string value associated with the identifier.</param>
public sealed record IdValueDto(int Id, string Value)
{
    /// <summary>
    /// Creates an empty identifier-value data transfer object.
    /// </summary>
    /// <return>
    /// An instance with an identifier of 0 and an empty string value.
    /// </return>
    public static IdValueDto Empty()
    {
        return new IdValueDto(0, string.Empty);
    }
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return Value;
    }
}