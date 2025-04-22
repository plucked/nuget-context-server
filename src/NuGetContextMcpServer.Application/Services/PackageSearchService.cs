using Microsoft.Extensions.Logging;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Abstractions.Dtos; // Updated namespace
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq; // Added for Count() and Enumerable.Empty
using System; // Added for Exception

namespace NuGetContextMcpServer.Application.Services;

public class PackageSearchService : IPackageSearchService // Interface now in Abstractions
{
    private readonly INuGetQueryService _nugetQueryService; // Interface now in Abstractions
    private readonly ILogger<PackageSearchService> _logger;

    public PackageSearchService(INuGetQueryService nugetQueryService, ILogger<PackageSearchService> logger)
    {
        _nugetQueryService = nugetQueryService;
        _logger = logger;
    }

    // Added skip and take parameters
    // Changed Dtos.PackageSearchResult to just PackageSearchResult (using updated namespace)
    public async Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease, int skip, int take, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Searching packages for term: {SearchTerm}, IncludePrerelease: {IncludePrerelease}, Skip: {Skip}, Take: {Take}", searchTerm, includePrerelease, skip, take);
        try
        {
            // Get all results from the query service first
            var allResults = await _nugetQueryService.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken);
            var resultCount = allResults.Count(); // Count before skipping/taking

            // Apply skip and take
            var pagedResults = allResults.Skip(skip).Take(take);

            _logger.LogInformation("Found {TotalCount} total packages, returning {PagedCount} packages for term: {SearchTerm}", resultCount, pagedResults.Count(), searchTerm);
            return pagedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching packages for term: {SearchTerm}", searchTerm);
            // Changed Dtos.PackageSearchResult to just PackageSearchResult (using updated namespace)
            return Enumerable.Empty<PackageSearchResult>();
        }
    }
}