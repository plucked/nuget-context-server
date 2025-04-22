using System.Threading;
using System.Threading.Tasks;
using NuGetContextMcpServer.Abstractions.Dtos;

namespace NuGetContextMcpServer.Abstractions.Interfaces;

/// <summary>
/// Defines the contract for a service that retrieves detailed metadata for NuGet packages.
/// </summary>
public interface IPackageMetadataService
{
    /// <summary>
    /// Retrieves detailed metadata for a specific NuGet package ID and optionally a specific version.
    /// If no version is specified, details for the latest version (stable or pre-release based on configuration/defaults) might be returned,
    /// or the service might require a version. The exact behavior depends on the implementation.
    /// </summary>
    /// <param name="packageId">The exact ID of the NuGet package.</param>
    /// <param name="version">The specific package version to retrieve details for. If null, the behavior is implementation-dependent (e.g., latest version).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// An asynchronous task that results in a <see cref="PackageDetailInfo"/> object containing the detailed metadata,
    /// or null if the package or specific version is not found or an error occurs.
    /// </returns>
    Task<PackageDetailInfo?> GetPackageDetailsAsync(string packageId, string? version = null, CancellationToken cancellationToken = default);
}