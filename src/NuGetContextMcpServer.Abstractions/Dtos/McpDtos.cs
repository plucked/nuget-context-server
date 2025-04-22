using System.Collections.Generic;

namespace NuGetContextMcpServer.Abstractions.Dtos; // Updated namespace

// DTO for AnalyzeProjectDependenciesAsync tool
public record AnalyzedDependency(
    string Id,
    string RequestedVersion, // Version specified in the project file
    string? LatestStableVersion,
    string? LatestVersion // Including prerelease
);

// PackageSearchResult is defined in NuGetContextMcpServer.Abstractions.Dtos

// DTO for GetLatestNuGetPackageVersionAsync tool
public record PackageVersionInfo(
    string PackageId,
    string LatestVersion
);

// Note: GetNuGetPackageVersionsAsync returns IEnumerable<string>, no specific DTO needed.