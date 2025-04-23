namespace NuGetContextMcpServer.Abstractions.Dtos;

/// <summary>
///     Represents a package found during a search operation.
/// </summary>
/// <param name="Id">The package ID.</param>
/// <param name="Version">The latest version string (normalized).</param>
/// <param name="Description">The package description.</param>
/// <param name="ProjectUrl">The project URL, if available.</param>
public record PackageSearchResult(
    string Id,
    string Version,
    string? Description,
    string? ProjectUrl
);