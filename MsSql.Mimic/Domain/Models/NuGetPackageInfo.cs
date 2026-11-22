namespace MsSql.Mimic.Domain.Models;

/// <summary>
/// Represents metadata for a NuGet package, including its package name and version.
/// </summary>
/// <param name="Name">The NuGet package name.</param>
/// <param name="Version">The NuGet package version.</param>
public sealed record NuGetPackageInfo(string Name, string Version);
