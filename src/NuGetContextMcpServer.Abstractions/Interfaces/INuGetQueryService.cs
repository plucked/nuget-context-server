using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetContextMcpServer.Abstractions.Dtos;

namespace NuGetContextMcpServer.Abstractions.Interfaces;

/// <summary>
///     Service for querying NuGet package information.
/// </summary>
public interface INuGetQueryService
{
    /// <summary>
    ///     Searches for NuGet packages based on a search term.
    /// </summary>
    /// <param name="searchTerm">The term to search for.</param>
    /// <param name="includePrerelease">Whether to include pre-release packages in the search results.</param>
    /// <param name="skip">The number of results to skip.</param>
    /// <param name="take">The number of results to take.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of package search results.</returns>
    Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease, int skip,
        int take, CancellationToken cancellationToken);

    /// <summary>
    ///     Gets all available versions for a given package ID.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of NuGet versions for the package.</returns>
    Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string packageId, CancellationToken cancellationToken);

    /// <summary>
    ///     Gets the latest stable version for a given package ID.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest stable NuGet version, or null if no stable version is found.</returns>
    Task<NuGetVersion?> GetLatestStableVersionAsync(string packageId, CancellationToken cancellationToken);

    /// <summary>
    ///     Gets the latest version (including pre-release) for a given package ID.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest NuGet version, or null if no version is found.</returns>
    Task<NuGetVersion?> GetLatestVersionAsync(string packageId, CancellationToken cancellationToken);

    /// <summary>
    ///     Gets the metadata for a specific package version.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="version">The specific package version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata for the specified version, or null if not found.</returns>
    Task<IPackageSearchMetadata?> GetPackageMetadataAsync(string packageId, NuGetVersion version,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Gets the metadata for the latest version of a package (stable or pre-release).
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="includePrerelease">Whether to include pre-release versions when determining the latest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata for the latest version, or null if not found.</returns>
    Task<IPackageSearchMetadata?> GetLatestPackageMetadataAsync(string packageId, bool includePrerelease,
        CancellationToken cancellationToken);
}