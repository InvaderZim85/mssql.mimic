using System.Collections.ObjectModel;

namespace MsSql.Mimic.Ui.Services;

/// <summary>
/// Provides extension methods for common collection-related operations used by the application.
/// </summary>
internal static class Extensions
{
    /// <summary>
    /// Creates an observable collection containing the elements from the specified sequence.
    /// </summary>
    /// <typeparam name="TEntity">The type of elements in the sequence.</typeparam>
    /// <param name="values">The sequence of values to convert.</param>
    /// <returns>An observable collection containing the elements from the specified sequence.</returns>
    public static ObservableCollection<TEntity> ToObservableCollection<TEntity>(this IEnumerable<TEntity> values)
    {
        return [.. values];
    }
}