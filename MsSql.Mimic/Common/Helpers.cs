using System.Diagnostics;

namespace MsSql.Mimic.Common;

/// <summary>
/// Provides common utility methods for application-level helper operations.
/// </summary>
internal static class Helpers
{
    /// <summary>
    /// Opens the specified URL using the operating system's default handler.
    /// </summary>
    /// <param name="url">The URL to open. If null, empty, or whitespace, no action is taken.</param>
    public static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}