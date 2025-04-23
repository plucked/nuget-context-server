using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using NuGetContextMcpServer.Abstractions.Dtos;
using NuGetContextMcpServer.Abstractions.Interfaces;

namespace NuGetContextMcpServer.Application.Mcp;

/// <summary>
///     Marker class for ILogger category specific to NuGetTools.
/// </summary>
public class NuGetToolLogger
{
}

/// <summary>
///     Provides MCP server tools related to NuGet package management and analysis.
///     These tools interact with underlying application services to perform operations.
/// </summary>
[McpServerToolType]
public static class NuGetTools
{
    /// <summary>
    ///     Analyzes a specified .NET solution (.sln) or project (.csproj) file to find its NuGet package dependencies
    ///     and their latest available versions on the configured feed.
    /// </summary>
    /// <param name="projectOrSolutionPath">The absolute path to the .sln or .csproj file accessible by the server.</param>
    /// <param name="analysisService">The service responsible for performing the project analysis.</param>
    /// <param name="logger">The logger for recording tool execution details and errors.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     An asynchronous task that results in an enumeration of <see cref="AnalyzedDependency" /> objects,
    ///     each representing a found dependency and its latest version. Returns an empty enumeration on error.
    /// </returns>
    [McpServerTool]
    [Description(
        "Analyzes a specified .NET solution (.sln) or project (.csproj) file to find its NuGet package dependencies and their latest available versions on the configured feed.")]
    public static async Task<IEnumerable<AnalyzedDependency>> AnalyzeProjectDependenciesAsync(
        [Description("The absolute path to the .sln or .csproj file accessible by the server.")]
        string projectOrSolutionPath,
        IProjectAnalysisService analysisService,
        ILogger<NuGetToolLogger> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("MCP Tool 'AnalyzeProjectDependenciesAsync' invoked for path: {Path}", projectOrSolutionPath);
        try
        {
            // Delegate the actual work to the application service layer
            return await analysisService.AnalyzeProjectAsync(projectOrSolutionPath, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing MCP Tool 'AnalyzeProjectDependenciesAsync' for path: {Path}",
                projectOrSolutionPath);
            return [];
        }
    }

    /// <summary>
    ///     Searches the configured NuGet feed for packages matching a given search term.
    /// </summary>
    /// <param name="searchTerm">The term to search for.</param>
    /// <param name="searchService">The service responsible for executing the package search.</param>
    /// <param name="logger">The logger for recording tool execution details and errors.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="includePrerelease">Whether to include pre-release package versions in the search results (default: false).</param>
    /// <param name="skip">Number of results to skip (for pagination, default: 0).</param>
    /// <param name="take">Maximum number of results to return (for pagination, default: 20).</param>
    /// <returns>
    ///     An asynchronous task that results in an enumeration of <see cref="PackageSearchResult" /> objects
    ///     matching the search criteria. Returns an empty enumeration on error.
    /// </returns>
    [McpServerTool]
    [Description("Searches the configured NuGet feed for packages matching a given search term.")]
    public static async Task<IEnumerable<PackageSearchResult>> SearchNuGetPackagesAsync(
        [Description("The term to search for.")]
        string searchTerm,
        IPackageSearchService searchService,
        ILogger<NuGetToolLogger> logger,
        CancellationToken cancellationToken,
        [Description("Whether to include pre-release package versions in the search results (default: false).")]
        bool includePrerelease = false,
        [Description("Number of results to skip (for pagination, default: 0).")]
        int skip = 0,
        [Description("Maximum number of results to return (for pagination, default: 20).")]
        int take = 20)
    {
        logger.LogDebug(
            "MCP Tool 'SearchNuGetPackagesAsync' invoked for term: {SearchTerm}, Skip: {Skip}, Take: {Take}",
            searchTerm, skip, take);
        try
        {
            return await searchService.SearchPackagesAsync(searchTerm, includePrerelease, skip, take,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing MCP Tool 'SearchNuGetPackagesAsync' for term: {SearchTerm}",
                searchTerm);
            return [];
        }
    }

    /// <summary>
    ///     Lists all available versions for a specific NuGet package ID from the configured feed.
    /// </summary>
    /// <param name="packageId">The exact ID of the NuGet package.</param>
    /// <param name="includePrerelease">Whether to include pre-release package versions (default: false).</param>
    /// <param name="versionService">The service responsible for retrieving package versions.</param>
    /// <param name="logger">The logger for recording tool execution details and errors.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     An asynchronous task that results in an enumeration of strings, each representing an available version
    ///     for the specified package. Returns an empty enumeration on error.
    /// </returns>
    [McpServerTool]
    [Description("Lists all available versions for a specific NuGet package ID from the configured feed.")]
    public static async Task<IEnumerable<string>> GetNuGetPackageVersionsAsync(
        [Description("The exact ID of the NuGet package.")]
        string packageId,
        [Description("Whether to include pre-release package versions (default: false).")]
        bool includePrerelease,
        IPackageVersionService versionService,
        ILogger<NuGetToolLogger> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("MCP Tool 'GetNuGetPackageVersionsAsync' invoked for package: {PackageId}", packageId);
        try
        {
            return await versionService.GetPackageVersionsAsync(packageId, includePrerelease, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing MCP Tool 'GetNuGetPackageVersionsAsync' for package: {PackageId}",
                packageId);
            return [];
        }
    }

    /// <summary>
    ///     Gets the latest version (stable or including pre-release) for a specific NuGet package ID from the configured feed.
    /// </summary>
    /// <param name="packageId">The exact ID of the NuGet package.</param>
    /// <param name="includePrerelease">
    ///     If true, returns the absolute latest version (including pre-release); otherwise,
    ///     returns the latest stable version (default: false).
    /// </param>
    /// <param name="versionService">The service responsible for retrieving the latest package version.</param>
    /// <param name="logger">The logger for recording tool execution details and errors.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    ///     An asynchronous task that results in a <see cref="PackageVersionInfo" /> object containing the latest version
    ///     details,
    ///     or null if the package is not found or an error occurs.
    /// </returns>
    [McpServerTool]
    [Description(
        "Gets the latest version (stable or including pre-release) for a specific NuGet package ID from the configured feed.")]
    public static async Task<PackageVersionInfo?> GetLatestNuGetPackageVersionAsync(
        [Description("The exact ID of the NuGet package.")]
        string packageId,
        [Description(
            "If true, returns the absolute latest version (including pre-release); otherwise, returns the latest stable version (default: false).")]
        bool includePrerelease,
        IPackageVersionService versionService,
        ILogger<NuGetToolLogger> logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("MCP Tool 'GetLatestNuGetPackageVersionAsync' invoked for package: {PackageId}", packageId);
        try
        {
            return await versionService.GetLatestPackageVersionAsync(packageId, includePrerelease, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing MCP Tool 'GetLatestNuGetPackageVersionAsync' for package: {PackageId}",
                packageId);
            return null;
        }
    }

    /// <summary>
    ///     Gets detailed metadata for a specific NuGet package ID, optionally for a specific version.
    /// </summary>
    /// <param name="packageId">The exact ID of the NuGet package.</param>
    /// <param name="metadataService">The service responsible for retrieving package metadata.</param>
    /// <param name="logger">The logger for recording tool execution details and errors.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="version">
    ///     Optional. The specific package version to retrieve details for. If null or empty, retrieves
    ///     details for the latest version.
    /// </param>
    /// <returns>
    ///     An asynchronous task that results in a <see cref="PackageDetailInfo" /> object containing the detailed metadata,
    ///     or null if the package or specific version is not found or an error occurs.
    /// </returns>
    [McpServerTool]
    [Description(
        "Gets detailed metadata (description, authors, URLs, etc.) for a specific NuGet package ID, optionally for a specific version.")]
    public static async Task<PackageDetailInfo?> GetNuGetPackageDetailsAsync(
        [Description("The exact ID of the NuGet package.")]
        string packageId,
        IPackageMetadataService metadataService,
        ILogger<NuGetToolLogger> logger,
        CancellationToken cancellationToken,
        [Description(
            "Optional. The specific package version (e.g., '1.2.3'). If omitted, fetches details for the latest version.")]
        string? version = null)
    {
        var targetVersion = string.IsNullOrWhiteSpace(version) ? null : version;
        logger.LogDebug("MCP Tool 'GetNuGetPackageDetailsAsync' invoked for package: {PackageId}, Version: {Version}",
            packageId, targetVersion ?? "Latest");

        try
        {
            return await metadataService.GetPackageDetailsAsync(packageId, targetVersion, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error executing MCP Tool 'GetNuGetPackageDetailsAsync' for package: {PackageId}, Version: {Version}",
                packageId, targetVersion ?? "Latest");
            return null;
        }
    }
}