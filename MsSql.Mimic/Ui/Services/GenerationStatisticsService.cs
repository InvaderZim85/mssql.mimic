using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Domain.Services;

using System.IO;
using System.Text.Json;

namespace MsSql.Mimic.Ui.Services;

/// <summary>
/// Manages persisted generation statistics for generated classes and tables.
/// </summary>
/// <remarks>
/// Tracks both cumulative totals and counts for the current application session, and provides asynchronous persistence operations for loading and saving the statistics data.
/// </remarks>
public sealed class GenerationStatisticsService
{
    /// <summary>
    /// Defines the JSON serializer settings used when reading and writing generation statistics.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Defines the path to the JSON file used to persist generation statistics.
    /// </summary>
    private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, "Statistics.json");

    /// <summary>
    /// Coordinates asynchronous access to generation statistics loading and saving operations.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Gets the current generation statistics data, including persisted totals and session counts.
    /// </summary>
    public GenerationStatistics Statistics { get; private set; } = new();

    /// <summary>
    /// Asynchronously loads generation statistics from the statistics file, if it exists.
    /// </summary>
    /// <param name="ct">A cancellation token that can be used to cancel the load operation.</param>
    /// <returns>A task that represents the asynchronous load operation.</returns>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);

        try
        {
            if (!File.Exists(_filePath))
                return;

            await using var stream = new FileStream(
                path: _filePath,
                mode: FileMode.Open,
                access: FileAccess.Read,
                share: FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            Statistics = await JsonSerializer.DeserializeAsync<GenerationStatistics>(
                utf8Json: stream,
                options: JsonOptions,
                cancellationToken: ct) ?? new GenerationStatistics();
            
            // Reset the session statistics
            Statistics.ResetSession();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Holds the cancellation token source for the currently scheduled debounced save operation.
    /// </summary>
    private CancellationTokenSource? _saveDebounceCts;

    /// <summary>
    /// Schedules generation statistics to be saved after a short debounce delay, canceling any pending scheduled save.
    /// </summary>
    public void ScheduleSave()
    {
        _saveDebounceCts?.Cancel();
        _saveDebounceCts?.Dispose();

        _saveDebounceCts = new CancellationTokenSource();
        var token = _saveDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), token);
                await SaveAsync(token);
            }
            catch (OperationCanceledException)
            {
                // Nothing to do here...
            }
        }, token);
    }
    
    /// <summary>
    /// Asynchronously saves the current generation statistics to the statistics file.
    /// </summary>
    /// <param name="ct">A cancellation token that can be used to cancel the save operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    private async Task SaveAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);

        try
        {
            var snapshot = GenerationStatistics.CreateSnapshot(Statistics);
            var directoryPath = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrWhiteSpace(directoryPath))
                Directory.CreateDirectory(directoryPath);

            var tempFilePath = _filePath + ".tmp";

            await using (var stream = new FileStream(
                             path: tempFilePath,
                             mode: FileMode.Create,
                             access: FileAccess.Write,
                             share: FileShare.None,
                             bufferSize: 4096,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    utf8Json: stream,
                    value: snapshot,
                    options: JsonOptions,
                    cancellationToken: ct);

                await stream.FlushAsync(ct);
            }

            File.Move(tempFilePath, _filePath, overwrite: true);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Records a class generation event for the specified table and connection, updating session and total generation statistics.
    /// </summary>
    /// <param name="connection">The connection profile that identifies the server and database containing the generated table.</param>
    /// <param name="table">The table entry whose schema and name identify the generated table.</param>
    public void TrackGeneration(ConnectionProfile connection, TableEntry table)
    {
        var tableKey = $"{connection.Server}|{connection.Database}|{table.Schema}|{table.Name}";

        Statistics.SessionGeneratedClassCount++;
        Statistics.SessionGeneratedTableKeys.Add(tableKey);
        Statistics.TotalGeneratedClassCount++;
        Statistics.TotalGeneratedTableKeys.Add(tableKey);
    }
}