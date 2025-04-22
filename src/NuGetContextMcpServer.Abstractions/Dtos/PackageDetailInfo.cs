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
    /// The package description.
    /// </summary>
    public string? Description { get; init; }

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

    // Consider adding DependencySets later if needed
    // public IEnumerable<PackageDependencyGroup> DependencySets { get; init; } = Enumerable.Empty<PackageDependencyGroup>();
}