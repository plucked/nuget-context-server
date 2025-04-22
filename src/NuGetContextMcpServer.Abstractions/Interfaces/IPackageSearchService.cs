using NuGetContextMcpServer.Abstractions.Dtos; // Updated using for DTOs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace

public interface IPackageSearchService
{
    /// <summary>
    /// Searches for NuGet packages based on a search term, with pagination.
    /// </summary>
    /// <param name="searchTerm">The term to search for.</param>
    /// <param name="includePrerelease">Whether to include pre-release packages.</param>
    /// <param name="skip">Number of results to skip (for pagination).</param>
    /// <param name="take">Number of results to take (for pagination).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of search results.</returns>
    // Changed Dtos.PackageSearchResult to just PackageSearchResult (using updated namespace)
    Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease, int skip, int take, CancellationToken cancellationToken);
}