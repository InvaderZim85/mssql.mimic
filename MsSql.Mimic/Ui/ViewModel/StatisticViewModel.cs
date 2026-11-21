using CommunityToolkit.Mvvm.ComponentModel;

using MsSql.Mimic.Domain.Services;

namespace MsSql.Mimic.Ui.ViewModel;

/// <summary>
/// Represents a view model that exposes generation statistics for display in the user interface.
/// </summary>
/// <remarks>
/// Maintains total and session-based counts for generated classes and tables, and supports refreshing those values from generation statistics data.
/// </remarks>
public sealed partial class StatisticViewModel : ObservableObject
{
    /// <summary>
    /// Gets the total number of generated classes.
    /// </summary>
    [ObservableProperty]
    public partial int TotalGeneratedClassCount { get; private set; }

    /// <summary>
    /// Gets the number of different tables for which classes have been generated.
    /// </summary>
    [ObservableProperty]
    public partial int TotalGeneratedTableCount { get; private set; }

    /// <summary>
    /// Gets the number of generated classes in the current session.
    /// </summary>
    [ObservableProperty]
    public partial int SessionGeneratedClassCount { get; private set; }

    /// <summary>
    /// Gets the number of different tables used for generation in the current session.
    /// </summary>
    [ObservableProperty]
    public partial int SessionGeneratedTableCount { get; private set; }

    /// <summary>
    /// Gets or sets the informational message displayed with the generation statistics.
    /// </summary>
    [ObservableProperty]
    public partial string InfoMessage { get; set; } = string.Empty;

    /// <summary>
    /// Updates the displayed generation statistics from the provided statistics data.
    /// </summary>
    /// <param name="statistics">The generation statistics used to update total and session class and table counts.</param>
    public void UpdateStatistics(GenerationStatistics statistics)
    {
        TotalGeneratedClassCount = statistics.TotalGeneratedClassCount;
        TotalGeneratedTableCount = statistics.TotalGeneratedTableCount;
        SessionGeneratedClassCount = statistics.SessionGeneratedClassCount;
        SessionGeneratedTableCount = statistics.SessionGeneratedTableCount;
    }
}