using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using NuGetContextMcpServer.Abstractions.Dtos;
using NuGetContextMcpServer.Abstractions.Interfaces;

namespace NuGetContextMcpServer.Application.Services;

/// <summary>
///     Provides functionality to retrieve version information for NuGet packages.
/// </summary>
public class PackageVersionService : IPackageVersionService
{
    private readonly INuGetQueryService _nugetQueryService;
    private readonly ILogger<PackageVersionService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PackageVersionService" /> class.
    /// </summary>
    /// <param name="nugetQueryService">The service used to query the NuGet feed.</param>
    /// <param name="logger">The logger for logging information and errors.</param>
    public PackageVersionService(INuGetQueryService nugetQueryService, ILogger<PackageVersionService> logger)
    {
        _nugetQueryService = nugetQueryService;
        _logger = logger;
    }

    /// <summary>
    ///     Gets all available versions for a specific package asynchronously, optionally including pre-release versions.
    /// </summary>
    /// <param name="packageId">The ID of the package.</param>
    /// <param name="includePrerelease">Indicates whether to include pre-release versions.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an enumerable collection
    ///     of version strings, sorted in descending order. Returns an empty collection if an error occurs.
    /// </returns>
    public async Task<IEnumerable<string>> GetPackageVersionsAsync(string packageId, bool includePrerelease,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting versions for package: {PackageId}, IncludePrerelease: {IncludePrerelease}",
            packageId, includePrerelease);
        try
        {
            var versions = await _nugetQueryService.GetAllVersionsAsync(packageId, cancellationToken);

            if (!includePrerelease) versions = versions.Where(v => !v.IsPrerelease);

            var versionStrings = versions
                .OrderByDescending(v => v)
                .Select(v => v.ToNormalizedString())
                .ToList();

            _logger.LogInformation("Found {Count} versions for package: {PackageId}", versionStrings.Count, packageId);
            return versionStrings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting versions for package: {PackageId}", packageId);
            return [];
        }
    }

    /// <summary>
    ///     Gets the latest available version (either stable or including pre-release) for a specific package asynchronously.
    /// </summary>
    /// <param name="packageId">The ID of the package.</param>
    /// <param name="includePrerelease">Indicates whether to include pre-release versions when determining the latest.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a <see cref="PackageVersionInfo" />
    ///     object with the latest version, or null if no version is found or an error occurs.
    /// </returns>
    public async Task<PackageVersionInfo?> GetLatestPackageVersionAsync(string packageId, bool includePrerelease,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting latest version for package: {PackageId}, IncludePrerelease: {IncludePrerelease}", packageId,
            includePrerelease);
        try
        {
            NuGetVersion? latestVersion;
            if (includePrerelease)
                latestVersion = await _nugetQueryService.GetLatestVersionAsync(packageId, cancellationToken);
            else
                latestVersion = await _nugetQueryService.GetLatestStableVersionAsync(packageId, cancellationToken);

            if (latestVersion != null)
            {
                _logger.LogInformation("Found latest version {Version} for package: {PackageId}",
                    latestVersion.ToNormalizedString(), packageId);
                return new PackageVersionInfo(packageId, latestVersion.ToNormalizedString());
            }

            _logger.LogWarning("Could not find latest version for package: {PackageId}", packageId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest version for package: {PackageId}", packageId);
            return null;
        }
    }
}