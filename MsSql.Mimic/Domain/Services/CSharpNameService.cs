using System.Text;
using System.Text.RegularExpressions;

namespace MsSql.Mimic.Domain.Services;

/// <summary>
/// Provides services for converting database object names into valid C# identifiers.
/// </summary>
/// <remarks>
/// Generates Pascal case names suitable for C# classes and properties, applying fallback names when source names are empty or otherwise cannot produce a valid identifier.
/// </remarks>
public sealed partial class CSharpNameService
{
    /// <summary>
    /// Creates a regular expression that matches one or more characters that are not letters or digits.
    /// </summary>
    /// <return>A regular expression used to split names into identifier parts.</return>
    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private partial Regex PropertyNameRegex();

    /// <summary>
    /// Converts a column name to a valid Pascal case property name, using a default property name when needed.
    /// </summary>
    /// <param name="columnName">The column name to convert into a property name.</param>
    /// <returns>The generated property name, prefixed with an underscore if it begins with a digit.</returns>
    public string ToPropertyName(string columnName)
    {
        return ToPascalCaseName(columnName, "Property");
    }

    /// <summary>
    /// Converts a table name to a valid Pascal case class name, using a default generated class name when needed.
    /// </summary>
    /// <param name="tableName">The table name to convert into a class name.</param>
    /// <return>The generated class name, prefixed with an underscore if it begins with a digit.</return>
    public string ToClassName(string tableName)
    {
        return ToPascalCaseName(tableName, "GeneratedClass");
    }

    /// <summary>
    /// Converts a name value to a Pascal case identifier, using a fallback when no valid name can be produced.
    /// </summary>
    /// <param name="value">The value to convert into a Pascal case name.</param>
    /// <param name="fallbackName">The name to return when <paramref name="value"/> is null, whitespace, or contains no usable name parts.</param>
    /// <return>The generated Pascal case name, prefixed with an underscore if it begins with a digit.</return>
    private string ToPascalCaseName(string value, string fallbackName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallbackName;

        var parts = PropertyNameRegex().Split(value.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part));

        var builder = new StringBuilder();

        foreach (var part in parts)
            builder.Append(ToPascalCasePart(part));

        var name = builder.Length == 0
            ? fallbackName
            : builder.ToString();

        if (char.IsDigit(name[0]))
            name = "_" + name;

        return name;
    }

    /// <summary>
    /// Converts a single name segment to Pascal case by uppercasing its first character.
    /// </summary>
    /// <param name="value">The name segment to convert.</param>
    /// <return>The converted Pascal case segment, or an empty string if <paramref name="value"/> is null or empty.</return>
    private static string ToPascalCasePart(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Length == 1)
            return value.ToUpperInvariant();

        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}