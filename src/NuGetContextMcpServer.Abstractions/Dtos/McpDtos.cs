namespace NuGetContextMcpServer.Abstractions.Dtos;

/// <summary>
///     Represents an analyzed dependency of a project.
/// </summary>
/// <param name="Id">The package ID of the dependency.</param>
/// <param name="RequestedVersion">The version of the dependency requested in the project file.</param>
/// <param name="LatestStableVersion">The latest stable version available for the package.</param>
/// <param name="LatestVersion">The latest version available for the package, including prerelease versions.</param>
public record AnalyzedDependency(
    string Id,
    string RequestedVersion,
    string? LatestStableVersion,
    string? LatestVersion
);

/// <summary>
///     Represents information about the latest version of a NuGet package.
/// </summary>
/// <param name="PackageId">The ID of the NuGet package.</param>
/// <param name="LatestVersion">The latest version available for the package.</param>
public record PackageVersionInfo(
    string PackageId,
    string LatestVersion
);