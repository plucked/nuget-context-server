using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGetContextMcpServer.Abstractions.Dtos;
using NuGetContextMcpServer.Abstractions.Interfaces;
using NuGet.Protocol.Core.Types; // Required for IPackageSearchMetadata
using NuGet.Versioning; // Required for NuGetVersion

namespace NuGetContextMcpServer.Application.Services;

/// <summary>
/// Service responsible for retrieving detailed metadata for NuGet packages.
/// </summary>
public class PackageMetadataService : IPackageMetadataService
{
    private readonly INuGetQueryService _nugetQueryService;
    private readonly ILogger<PackageMetadataService> _logger;

    public PackageMetadataService(INuGetQueryService nugetQueryService, ILogger<PackageMetadataService> logger)
    {
        _nugetQueryService = nugetQueryService ?? throw new ArgumentNullException(nameof(nugetQueryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PackageDetailInfo?> GetPackageDetailsAsync(string packageId, string? version = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to get package details for {PackageId}, Version: {Version}", packageId, version ?? "Latest");

        try
        {
            IPackageSearchMetadata? metadata;
            if (string.IsNullOrEmpty(version))
            {
                // If no version specified, get metadata for the latest version (stable or prerelease based on query service impl)
                 metadata = await _nugetQueryService.GetLatestPackageMetadataAsync(packageId, true, cancellationToken); // Assuming true includes prerelease for "latest"
                 if (metadata == null)
                 {
                     // Maybe try latest stable if latest overall wasn't found? Or just return null.
                     // Let's try stable as a fallback for now.
                     _logger.LogDebug("Latest version (including prerelease) not found for {PackageId}, trying latest stable.", packageId);
                     metadata = await _nugetQueryService.GetLatestPackageMetadataAsync(packageId, false, cancellationToken);
                 }
            }
            else
            {
                // If version is specified, try to parse it and get metadata for that specific version
                if (!NuGetVersion.TryParse(version, out var nugetVersion))
                {
                    _logger.LogWarning("Invalid version format provided: {Version}", version);
                    return null; // Invalid version format
                }
                metadata = await _nugetQueryService.GetPackageMetadataAsync(packageId, nugetVersion, cancellationToken);
            }


            if (metadata == null)
            {
                _logger.LogWarning("Package metadata not found for {PackageId}, Version: {Version}", packageId, version ?? "Not Specified");
                return null;
            }

            // Map the IPackageSearchMetadata to our PackageDetailInfo DTO
            return MapMetadataToDetailInfo(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving package details for {PackageId}, Version: {Version}", packageId, version ?? "Latest");
            return null; // Return null on error
        }
    }

    private static PackageDetailInfo MapMetadataToDetailInfo(IPackageSearchMetadata metadata)
    {
        // The Identity property contains both Id and Version
        return new PackageDetailInfo
        {
            Id = metadata.Identity.Id,
            Version = metadata.Identity.Version.ToNormalizedString(), // Use normalized string representation
            Description = metadata.Description,
            Authors = metadata.Authors, // Usually a comma-separated string
            ProjectUrl = metadata.ProjectUrl?.ToString(),
            LicenseUrl = metadata.LicenseUrl?.ToString(),
            IconUrl = metadata.IconUrl?.ToString(),
            Tags = metadata.Tags, // Usually a space or comma-separated string
            Published = metadata.Published,
            IsListed = metadata.IsListed
            // Note: DependencySets are not directly available on IPackageSearchMetadata easily.
            // Getting full dependency info requires PackageMetadataResource.GetMetadataAsync(identity)
            // which might be a separate, more involved operation if needed later.
        };
    }
}