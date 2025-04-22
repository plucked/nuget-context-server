using System;
using System.Collections.Generic;

namespace NuGetContextMcpServer.Abstractions.Dtos;

/// <summary>
/// Represents detailed information about a specific NuGet package version.
/// </summary>
public class PackageDetailInfo
{
    /// <summary>
    /// The package ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The specific package version.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// The user-friendly title of the package.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// The package description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// A short summary of the package's purpose.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// The package authors (comma-separated).
    /// </summary>
    public string? Authors { get; init; } // NuGet API often returns this as a single string

    /// <summary>
    /// URL for the package's project page.
    /// </summary>
    public string? ProjectUrl { get; init; }

    /// <summary>
    /// URL for the package's license information.
    /// </summary>
    public string? LicenseUrl { get; init; }

    /// <summary>
    /// URL for the package's icon.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Tags associated with the package (space-separated or comma-separated).
    /// </summary>
    public string? Tags { get; init; } // NuGet API often returns this as a single string

    /// <summary>
    /// The date and time the package version was published.
    /// </summary>
    public DateTimeOffset? Published { get; init; }

    /// <summary>
    /// Indicates if the package is listed in search results.
    /// </summary>
    public bool IsListed { get; init; }

    /// <summary>
    /// The total download count for the package (across all versions, typically).
    /// Note: The exact meaning (specific version vs. all versions) might depend on the NuGet API endpoint used.
    /// </summary>
    public long? DownloadCount { get; init; }

    /// <summary>
    /// Indicates if the package requires the user to accept the license before installation.
    /// </summary>
    public bool RequireLicenseAcceptance { get; init; }

    // Consider adding DependencySets later if needed
    // public IEnumerable<PackageDependencyGroup> DependencySets { get; init; } = Enumerable.Empty<PackageDependencyGroup>();
}