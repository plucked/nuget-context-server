using NuGetContextMcpServer.Abstractions.Dtos;

namespace NuGetContextMcpServer.Abstractions.Interfaces;

/// <summary>
///     Service for retrieving package version information from NuGet.
/// </summary>
public interface IPackageVersionService
{
    /// <summary>
    ///     Gets all available versions for a given package ID.
    /// </summary>
    /// <param name="packageId">The ID of the package.</param>
    /// <param name="includePrerelease">Whether to include prerelease versions.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A collection of package versions.</returns>
    Task<IEnumerable<string>> GetPackageVersionsAsync(string packageId, bool includePrerelease,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Gets the latest version for a given package ID.
    /// </summary>
    /// <param name="packageId">The ID of the package.</param>
    /// <param name="includePrerelease">Whether to consider prerelease versions as the latest.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The latest package version, or null if the package is not found.</returns>
    Task<PackageVersionInfo?> GetLatestPackageVersionAsync(string packageId, bool includePrerelease,
        CancellationToken cancellationToken);
}