using Microsoft.Extensions.Logging;
using NuGetContextMcpServer.Abstractions.Interfaces;
using NuGetContextMcpServer.Abstractions.Dtos;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace NuGetContextMcpServer.Application.Services;

/// <summary>
/// Provides functionality to search for NuGet packages using the underlying query service.
/// </summary>
public class PackageSearchService : IPackageSearchService
{
    private readonly INuGetQueryService _nugetQueryService;
    private readonly ILogger<PackageSearchService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageSearchService"/> class.
    /// </summary>
    /// <param name="nugetQueryService">The service used to query the NuGet feed.</param>
    /// <param name="logger">The logger for logging information and errors.</param>
    public PackageSearchService(INuGetQueryService nugetQueryService, ILogger<PackageSearchService> logger)
    {
        _nugetQueryService = nugetQueryService;
        _logger = logger;
    }

    /// <summary>
    /// Searches for NuGet packages asynchronously based on a search term, applying pagination.
    /// </summary>
    /// <param name="searchTerm">The search term to use for finding packages.</param>
    /// <param name="includePrerelease">Indicates whether to include pre-release packages in the search results.</param>
    /// <param name="skip">The number of results to skip for pagination.</param>
    /// <param name="take">The maximum number of results to take for pagination.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an enumerable collection
    /// of <see cref="PackageSearchResult"/> matching the search criteria and pagination options.
    /// Returns an empty collection if an error occurs during the search.
    /// </returns>
    public async Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease, int skip, int take, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Searching packages for term: {SearchTerm}, IncludePrerelease: {IncludePrerelease}, Skip: {Skip}, Take: {Take}", searchTerm, includePrerelease, skip, take);
        try
        {
            // The INuGetQueryService is expected to handle the skip/take logic.
            var pagedResults = await _nugetQueryService.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken);
            var resultCount = pagedResults.Count();
            _logger.LogInformation("Returning {PagedCount} packages for term: {SearchTerm}", resultCount, searchTerm);
            return pagedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching packages for term: {SearchTerm}", searchTerm);
            return Enumerable.Empty<PackageSearchResult>();
        }
    }
}