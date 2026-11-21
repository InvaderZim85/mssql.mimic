namespace MsSql.Mimic.Domain.Services;

/// <summary>
/// Represents aggregate and per-session statistics collected during code generation.
/// </summary>
/// <remarks>
/// Tracks generated class counts and generated table keys for both the overall lifetime
/// of the statistics instance and the current generation session. Table counts are derived
/// from the number of unique table keys recorded in the corresponding key sets.
/// </remarks>
public sealed class GenerationStatistics
{
    /// <summary>
    /// Gets or sets the total number of classes generated across all sessions.
    /// </summary>
    public int TotalGeneratedClassCount { get; set; }

    /// <summary>
    /// Gets or sets the unique table keys generated across all sessions.
    /// </summary>
    public HashSet<string> TotalGeneratedTableKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of classes generated during the current session.
    /// </summary>
    public int SessionGeneratedClassCount { get; set; }

    /// <summary>
    /// Gets or sets the unique table keys generated during the current session.
    /// </summary>
    public HashSet<string> SessionGeneratedTableKeys { get; set; } = [];

    /// <summary>
    /// Gets the total number of unique tables generated across all sessions.
    /// </summary>
    public int TotalGeneratedTableCount => TotalGeneratedTableKeys.Count;

    /// <summary>
    /// Gets the number of unique tables generated during the current session.
    /// </summary>
    public int SessionGeneratedTableCount => SessionGeneratedTableKeys.Count;

    /// <summary>
    /// Creates a new generation statistics instance initialized from the specified statistics.
    /// </summary>
    /// <param name="statistics">The generation statistics to copy from.</param>
    /// <returns>A new generation statistics instance containing the copied statistic values.</returns>
    public static GenerationStatistics CreateSnapshot(GenerationStatistics statistics)
    {
        return new GenerationStatistics
        {
            TotalGeneratedClassCount = statistics.TotalGeneratedClassCount,
            TotalGeneratedTableKeys = statistics.TotalGeneratedTableKeys,
            SessionGeneratedClassCount = statistics.SessionGeneratedClassCount,
            SessionGeneratedTableKeys = statistics.SessionGeneratedTableKeys
        };
    }

    /// <summary>
    /// Resets the current generation session statistics while preserving accumulated total statistics.
    /// </summary>
    public void ResetSession()
    {
        SessionGeneratedClassCount = 0;
        SessionGeneratedTableKeys.Clear();
    }
}