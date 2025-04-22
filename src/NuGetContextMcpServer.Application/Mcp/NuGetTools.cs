using ModelContextProtocol; // Corrected namespace
using ModelContextProtocol.Server; // Added for McpTools/McpTool attributes
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Abstractions.Dtos; // Updated namespace
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System; // Added for Exception
using System.Linq; // Added for Enumerable.Empty

namespace NuGetContextMcpServer.Application.Mcp; // Updated namespace

/// <summary>Marker class for ILogger category specific to NuGetTools.</summary>
public class NuGetToolLogger { } // Made public

/// <summary>
/// Provides MCP server tools related to NuGet package management and analysis.
/// These tools interact with underlying application services to perform operations.
/// </summary>
[McpServerToolType] // Corrected attribute name based on sample
public static class NuGetTools
{
    /// <summary>
    /// Analyzes a specified .NET solution (.sln) or project (.csproj) file to find its NuGet package dependencies
    /// and their latest available versions on the configured feed.
    /// </summary>
    /// <param name="projectOrSolutionPath">The absolute path to the .sln or .csproj file accessible by the server.</param>
    /// <param name="analysisService">The service responsible for performing the project analysis.</param>
    /// <param name="logger">The logger for recording tool execution details and errors.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// An asynchronous task that results in an enumeration of <see cref="AnalyzedDependency"/> objects,
    /// each representing a found dependency and its latest version. Returns an empty enumeration on error.
    /// </returns>
    [McpServerTool] // Corrected attribute name based on sample
    [Description("Analyzes a specified .NET solution (.sln) or project (.csproj) file to find its NuGet package dependencies and their latest available versions on the configured feed.")]
    public static async Task<IEnumerable<AnalyzedDependency>> AnalyzeProjectDependenciesAsync(
        [Description("The absolute path to the .sln or .csproj file accessible by the server.")] string projectOrSolutionPath,
        IProjectAnalysisService analysisService, // Injected via DI
        ILogger<NuGetToolLogger> logger, // Corrected logger type
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
            logger.LogError(ex, "Error executing MCP Tool 'AnalyzeProjectDependenciesAsync' for path: {Path}", projectOrSolutionPath);
            // Return empty on error as per current design
            return Enumerable.Empty<AnalyzedDependency>();
        }
    }

    /// <summary>
    /// Searches the configured NuGet feed for packages matching a given search term.
    /// </summary>
    /// <param name="searchTerm">The term to search for.</param>
    /// <param name="searchService">The service responsible for executing the package search.</param>
    /// <param name="logger">The logger for recording tool execution details and errors.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <param name="includePrerelease">Whether to include pre-release package versions in the search results (default: false).</param>
    /// <param name="skip">Number of results to skip (for pagination, default: 0).</param>
    /// <param name="take">Maximum number of results to return (for pagination, default: 20).</param>
    /// <returns>
    /// An asynchronous task that results in an enumeration of <see cref="PackageSearchResult"/> objects
    /// matching the search criteria. Returns an empty enumeration on error.
    /// </returns>
    [McpServerTool] // Corrected attribute name based on sample
    [Description("Searches the configured NuGet feed for packages matching a given search term.")]
    public static async Task<IEnumerable<PackageSearchResult>> SearchNuGetPackagesAsync(
        // Required parameters first
        [Description("The term to search for.")] string searchTerm,
        IPackageSearchService searchService, // Interface now in Abstractions
        ILogger<NuGetToolLogger> logger, // Corrected logger type
        CancellationToken cancellationToken,
        // Optional parameters last
        [Description("Whether to include pre-release package versions in the search results (default: false).")] bool includePrerelease = false,
        [Description("Number of results to skip (for pagination, default: 0).")] int skip = 0,
        [Description("Maximum number of results to return (for pagination, default: 20).")] int take = 20)
    {
         logger.LogDebug("MCP Tool 'SearchNuGetPackagesAsync' invoked for term: {SearchTerm}, Skip: {Skip}, Take: {Take}", searchTerm, skip, take); // Added skip/take to log
         try
         {
            // Pass skip and take to the service call
            return await searchService.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, cancellationToken);
         }
         catch (Exception ex)
         {
             logger.LogError(ex, "Error executing MCP Tool 'SearchNuGetPackagesAsync' for term: {SearchTerm}", searchTerm);
             // Return empty on error as per current design
             return Enumerable.Empty<PackageSearchResult>();
         }
     }
 
     /// <summary>
     /// Lists all available versions for a specific NuGet package ID from the configured feed.
     /// </summary>
     /// <param name="packageId">The exact ID of the NuGet package.</param>
     /// <param name="includePrerelease">Whether to include pre-release package versions (default: false).</param>
     /// <param name="versionService">The service responsible for retrieving package versions.</param>
     /// <param name="logger">The logger for recording tool execution details and errors.</param>
     /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
     /// <returns>
     /// An asynchronous task that results in an enumeration of strings, each representing an available version
     /// for the specified package. Returns an empty enumeration on error.
     /// </returns>
     [McpServerTool] // Corrected attribute name based on sample
     [Description("Lists all available versions for a specific NuGet package ID from the configured feed.")]
     public static async Task<IEnumerable<string>> GetNuGetPackageVersionsAsync(
        [Description("The exact ID of the NuGet package.")] string packageId,
        [Description("Whether to include pre-release package versions (default: false).")] bool includePrerelease,
        IPackageVersionService versionService, // Interface now in Abstractions
        ILogger<NuGetToolLogger> logger, // Corrected logger type
        CancellationToken cancellationToken)
    {
        logger.LogDebug("MCP Tool 'GetNuGetPackageVersionsAsync' invoked for package: {PackageId}", packageId);
        try
        {
            return await versionService.GetPackageVersionsAsync(packageId, includePrerelease, cancellationToken);
        }
        catch (Exception ex)
        {
             logger.LogError(ex, "Error executing MCP Tool 'GetNuGetPackageVersionsAsync' for package: {PackageId}", packageId);
             // Return empty on error as per current design
             return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Gets the latest version (stable or including pre-release) for a specific NuGet package ID from the configured feed.
    /// </summary>
    /// <param name="packageId">The exact ID of the NuGet package.</param>
    /// <param name="includePrerelease">If true, returns the absolute latest version (including pre-release); otherwise, returns the latest stable version (default: false).</param>
    /// <param name="versionService">The service responsible for retrieving the latest package version.</param>
    /// <param name="logger">The logger for recording tool execution details and errors.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// An asynchronous task that results in a <see cref="PackageVersionInfo"/> object containing the latest version details,
    /// or null if the package is not found or an error occurs.
    /// </returns>
    [McpServerTool] // Corrected attribute name based on sample
    [Description("Gets the latest version (stable or including pre-release) for a specific NuGet package ID from the configured feed.")]
    public static async Task<PackageVersionInfo?> GetLatestNuGetPackageVersionAsync(
         [Description("The exact ID of the NuGet package.")] string packageId,
         [Description("If true, returns the absolute latest version (including pre-release); otherwise, returns the latest stable version (default: false).")] bool includePrerelease,
         IPackageVersionService versionService, // Interface now in Abstractions
         ILogger<NuGetToolLogger> logger, // Corrected logger type
         CancellationToken cancellationToken)
    {
        logger.LogDebug("MCP Tool 'GetLatestNuGetPackageVersionAsync' invoked for package: {PackageId}", packageId);
        try
        {
            return await versionService.GetLatestPackageVersionAsync(packageId, includePrerelease, cancellationToken);
        }
        catch (Exception ex)
        {
             logger.LogError(ex, "Error executing MCP Tool 'GetLatestNuGetPackageVersionAsync' for package: {PackageId}", packageId);
             // Return null on error as per current design
             return null;
        }
    }
}