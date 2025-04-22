using NuGet.Protocol.Core.Types; // Added for IPackageSearchMetadata
using NuGet.Versioning;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGetContextMcpServer.Abstractions.Dtos; // Updated using for DTOs

namespace NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace

// Removed placeholder DTO

public interface INuGetQueryService
{
    Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease, int skip, int take, CancellationToken cancellationToken);
    Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string packageId, CancellationToken cancellationToken);
    Task<NuGetVersion?> GetLatestStableVersionAsync(string packageId, CancellationToken cancellationToken);
    Task<NuGetVersion?> GetLatestVersionAsync(string packageId, CancellationToken cancellationToken); // Includes prerelease

    /// <summary>
    /// Gets the metadata for a specific package version.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="version">The specific package version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata for the specified version, or null if not found.</returns>
    Task<IPackageSearchMetadata?> GetPackageMetadataAsync(string packageId, NuGetVersion version, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the metadata for the latest version of a package (stable or pre-release).
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="includePrerelease">Whether to include pre-release versions when determining the latest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata for the latest version, or null if not found.</returns>
    Task<IPackageSearchMetadata?> GetLatestPackageMetadataAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken);
}