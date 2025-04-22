using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGetContextMcpServer.Abstractions.Dtos; // Updated using for DTOs

namespace NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace

// Removed placeholder DTO

public interface IPackageVersionService
{
    Task<IEnumerable<string>> GetPackageVersionsAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken);
    // Changed Mcp.PackageVersionInfo to just PackageVersionInfo (using updated namespace)
    Task<PackageVersionInfo?> GetLatestPackageVersionAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken);
}