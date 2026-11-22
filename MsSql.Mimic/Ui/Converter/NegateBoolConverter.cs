using System.Globalization;
using System.Windows.Data;

namespace MsSql.Mimic.Ui.Converter;

/// <summary>
/// Converts Boolean values to their logical negation for data binding scenarios.
/// </summary>
/// <remarks>
/// Returns <see langword="true"/> when the input value is <see langword="false"/>,
/// and <see langword="false"/> when the input value is <see langword="true"/>.
/// Non-Boolean values are treated as invalid input and produce <see langword="false"/>.
/// </remarks>
internal sealed class NegateBoolConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return ConvertBool(value);
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return ConvertBool(value);
    }

    /// <summary>
    /// Converts the specified value to its negated Boolean value.
    /// </summary>
    /// <param name="value">The value to convert. If the value is not a Boolean, the result is false.</param>
    /// <returns>true when <paramref name="value"/> is false; false when <paramref name="value"/> is true or is not a Boolean.</returns>
    private static bool ConvertBool(object? value)
    {
        if (value is not bool tmpBool)
            return false;

        return !tmpBool;
    }
}