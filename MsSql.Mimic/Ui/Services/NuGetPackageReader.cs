using MsSql.Mimic.Domain.Models;

using Serilog;

using System.IO;
using System.Reflection;
using System.Text.Json;

namespace MsSql.Mimic.Ui.Services;

/// <summary>
/// Provides functionality for reading NuGet package metadata from the application's dependency context file.
/// </summary>
/// <remarks>
/// Reads the entry assembly .deps.json file, extracts libraries marked as NuGet packages, and exposes their names and versions as package metadata.
/// </remarks>
public sealed class NuGetPackageReader
{
    /// <summary>
    /// Gets the NuGet package dependencies listed in the application's dependency context file.
    /// </summary>
    /// <returns>A read-only list of NuGet package names and versions, sorted by package name and version, or an empty list if no package dependencies can be found.</returns>
    public IReadOnlyList<NuGetPackageInfo> GetNuGetPackages()
    {
        var depsJsonPath = GetDepsJsonpath();

        if (!File.Exists(depsJsonPath))
            return [];

        using var stream = File.OpenRead(depsJsonPath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("libraries", out var librariesElement))
            return [];

        var packages = new List<NuGetPackageInfo>();

        foreach (var library in librariesElement.EnumerateObject())
        {
            var libraryName = library.Name;
            var libraryData = library.Value;
            
            if (!libraryData.TryGetProperty("type", out var typeElement))
                continue;

            var type = typeElement.GetString();
            
            if (!string.Equals(type, "package", StringComparison.OrdinalIgnoreCase))
                continue;

            var package = ParsePackageName(libraryName);

            if (package is not null)
                packages.Add(package);
        }

        return
        [
            .. packages
                .Where(package => !ShouldHidePackage(package.Name))
                .OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>
    /// Gets the path to the dependency context file for the entry assembly.
    /// </summary>
    /// <returns>The full path to the entry assembly .deps.json file, or an empty string if the application name cannot be determined.</returns>
    private static string GetDepsJsonpath()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var applicationName = entryAssembly?.GetName().Name;

        if (string.IsNullOrWhiteSpace(applicationName))
        {
            Log.Warning("The application name could not be determined.");
            return string.Empty;
        }

        return Path.Combine(AppContext.BaseDirectory, $"{applicationName}.deps.json");
    }

    /// <summary>
    /// Parses a library name into NuGet package metadata.
    /// </summary>
    /// <param name="libraryName">The library name containing the package name and version separated by the last slash character.</param>
    /// <returns>The parsed NuGet package information, or null if the library name is not in a valid package/version format.</returns>
    private static NuGetPackageInfo? ParsePackageName(string libraryName)
    {
        var separatorIndex = libraryName.LastIndexOf('/');

        if (separatorIndex <= 0 || separatorIndex >= libraryName.Length - 1)
            return null;

        var name = libraryName[..separatorIndex];
        var version = libraryName[(separatorIndex + 1)..];

        return new NuGetPackageInfo(name, version);
    }

    /// <summary>
    /// Determines whether a NuGet package should be excluded from the displayed package list based on its name prefix.
    /// </summary>
    /// <param name="packageName">The name of the NuGet package to evaluate.</param>
    /// <returns><see langword="true"/> if the package name starts with a hidden framework or runtime prefix; otherwise, <see langword="false"/>.</returns>
    private static bool ShouldHidePackage(string packageName)
    {
        return packageName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
               packageName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
               packageName.StartsWith("runtime.", StringComparison.OrdinalIgnoreCase);
    }
}