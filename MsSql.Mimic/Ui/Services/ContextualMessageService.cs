using System.Reflection;

namespace MsSql.Mimic.Ui.Services;

/// <summary>
/// Provides contextual user-facing messages for application events such as schema loading,
/// repeated table generation, and application startup.
/// </summary>
/// <remarks>
/// This service returns optional messages when predefined conditions are met, including
/// large schema loads, specific repeat counts for table generation operations, and
/// date-based startup events. It maintains in-memory operation counts for generated
/// schema and table combinations for the lifetime of the service instance.
/// </remarks>
public sealed class ContextualMessageService
{
    /// <summary>
    /// Tracks the number of times each operation has been performed, keyed by operation identifier.
    /// </summary>
    private readonly Dictionary<string, int> _operationCounts = [];

    /// <summary>
    /// Contains the predefined messages that may be displayed during application startup.
    /// </summary>
    private static readonly List<string> StartupMessages =
    [
        "Be thankful for the pain. It means you're still alive.",
        "Adapt. Analyze. Replicate.",
        "No Mimics were harmed during schema extraction.",
        "The database is learning...",
        "Today is a good day to generate DTOs.",
        "Loop counter increased",
        "Resetting timeline...",
        "No matter how many times you generate it, the schema always comes back.",
        "Built for those doomed to fight legacy databases again and again."
    ];

    /// <summary>
    /// Gets a contextual message based on the number of schema items loaded.
    /// </summary>
    /// <param name="itemCount">The number of schema items that were loaded.</param>
    /// <returns>
    /// A contextual message when the loaded schema item count reaches the threshold, or <see langword="null"/> when no message should be shown.
    /// </returns>
    public string? GetSchemaLoadedMessage(int itemCount)
    {
        return itemCount < 1_000
            ? null : 
            "Target-rich environment detected - possible 'Omega' present.";
    }

    /// <summary>
    /// Gets a contextual message for repeated generation of the specified table.
    /// </summary>
    /// <param name="schema">The schema name that identifies the table namespace.</param>
    /// <param name="table">The table name whose generation count is being tracked.</param>
    /// <returns>
    /// A contextual message when the specified table reaches a tracked generation count, or <see langword="null"/> when no message should be shown.
    /// </returns>
    public string? GetGenerationMessage(string schema, string table)
    {
        var key = $"{schema}.{table}";

        _operationCounts.TryGetValue(key, out var count);
        _operationCounts[key] = count + 1;

        return (count + 1) switch
        {
            5 => "Do you believe in time loops?",
            10 => "The loop is becoming familiar.",
            20 => "Strange... I feel like we've generated this table before",
            30 => "Welcome back. Again.",
            50 => "There is one thing worse than finding a bug. Finding the same bug every day.",
            _ => null
        };
    }

    /// <summary>
    /// Gets the startup message to display for the current date.
    /// </summary>
    /// <returns>
    /// The fixed cycle-reset message on April 1, a startup message when startup messaging is triggered, or <see langword="null"/> when no startup message should be shown.
    /// </returns>
    public string? GetStartupMessage()
    {
        var now = DateTimeOffset.Now;
        return now.Month != 4 && now.Day != 1
            ? PickupStartupMessage()
            : "Reset complete. Another cycle begins.";
    }

    /// <summary>
    /// Selects a startup message at random when startup messaging is triggered.
    /// </summary>
    /// <returns>
    /// A randomly selected startup message, or <see langword="null"/> when no startup message should be shown.
    /// </returns>
    private static string? PickupStartupMessage()
    {
        var shouldMessage = Random.Shared.Next(5) == 0;

        return !shouldMessage 
            ? null : 
            StartupMessages[Random.Shared.Next(StartupMessages.Count)];
    }

    /// <summary>
    /// Gets an informational message containing the application version and current replication engine status details.
    /// </summary>
    /// <returns>
    /// A formatted informational message for display to the user.
    /// </returns>
    public string GetInfoMessage()
    {
        return
            $"""
             MsSql.Mimic v{Assembly.GetExecutingAssembly().GetName().Version}

             All data structures are equal, but some are more equal than others.

             Replication Engine Status:
             OPERATIONAL

             Loop Counter:
             UNKNOWN
             """;
    }

    /// <summary>
    /// Gets a formatted statistics message summarizing used tables and generated classes.
    /// </summary>
    /// <param name="totalTables">The total number of tables that were used.</param>
    /// <param name="totalClasses">The total number of classes that were generated.</param>
    /// <returns>
    /// A formatted information message with the current statistics.
    /// </returns>
    public string GetStatisticsInfoMessage(int totalTables, int totalClasses)
    {
        if (totalTables is 127 or 420 or 666 or 1337 ||
            totalClasses is 127 or 420 or 666 or 1337)
        {
            return
                $"""
                 === MIMIC ENGAGEMENT REPORT ===

                 - Targets scanned: {totalTables:N0}
                 - Structures replicated: {totalClasses:N0}
                 - Casualties: 0

                 Humanity survives another day.
                 """;
        }

        return "Analyze. Adapt. Replicate.";
    }
}