using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Abstractions.Dtos; // Updated namespace
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System; // Added for Exception

namespace NuGetContextMcpServer.Application.Services;

public class PackageVersionService : IPackageVersionService // Interface now in Abstractions
{
    private readonly INuGetQueryService _nugetQueryService; // Interface now in Abstractions
    private readonly ILogger<PackageVersionService> _logger;

    public PackageVersionService(INuGetQueryService nugetQueryService, ILogger<PackageVersionService> logger)
    {
        _nugetQueryService = nugetQueryService;
        _logger = logger;
    }

    // Return type IEnumerable<string> remains the same
    public async Task<IEnumerable<string>> GetPackageVersionsAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting versions for package: {PackageId}, IncludePrerelease: {IncludePrerelease}", packageId, includePrerelease);
        try
        {
            var versions = await _nugetQueryService.GetAllVersionsAsync(packageId, cancellationToken);

            if (!includePrerelease)
            {
                versions = versions.Where(v => !v.IsPrerelease);
            }

            // Order descending by semantic version
            var versionStrings = versions
                .Select(v => v.ToNormalizedString())
                .OrderByDescending(v => NuGetVersion.Parse(v)) // Use NuGetVersion for robust sorting
                .ToList();

            _logger.LogInformation("Found {Count} versions for package: {PackageId}", versionStrings.Count, packageId);
            return versionStrings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting versions for package: {PackageId}", packageId);
            return Enumerable.Empty<string>();
        }
    }

    // Changed Mcp.PackageVersionInfo to just PackageVersionInfo (using updated namespace)
    public async Task<PackageVersionInfo?> GetLatestPackageVersionAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting latest version for package: {PackageId}, IncludePrerelease: {IncludePrerelease}", packageId, includePrerelease);
        try
        {
            NuGetVersion? latestVersion;
            if (includePrerelease)
            {
                latestVersion = await _nugetQueryService.GetLatestVersionAsync(packageId, cancellationToken);
            }
            else
            {
                latestVersion = await _nugetQueryService.GetLatestStableVersionAsync(packageId, cancellationToken);
            }

            if (latestVersion != null)
            {
                _logger.LogInformation("Found latest version {Version} for package: {PackageId}", latestVersion.ToNormalizedString(), packageId);
                return new PackageVersionInfo(packageId, latestVersion.ToNormalizedString());
            }
            else
            {
                _logger.LogWarning("Could not find latest version for package: {PackageId}", packageId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest version for package: {PackageId}", packageId);
            return null;
        }
    }
}