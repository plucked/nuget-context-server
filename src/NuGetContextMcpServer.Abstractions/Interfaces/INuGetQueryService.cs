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
    // Potentially add GetPackageMetadataAsync if needed later
}