using System.Collections.ObjectModel;

namespace MsSql.Mimic.Ui;

internal static class Extensions
{
    public static ObservableCollection<TEntity> ToObservableCollection<TEntity>(this IEnumerable<TEntity> values)
    {
        return [.. values];
    }
}