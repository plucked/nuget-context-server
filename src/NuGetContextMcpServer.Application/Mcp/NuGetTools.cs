using ModelContextProtocol; // Corrected namespace
using ModelContextProtocol.Server; // Added for McpTools/McpTool attributes
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Abstractions.Dtos; // Updated namespace
// Removed using NuGetContextMcpServer.Application.Mcp; as DTOs are now in Abstractions.Dtos
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System; // Added for Exception
using System.Linq; // Added for Enumerable.Empty

namespace NuGetContextMcpServer.Application.Mcp; // Updated namespace

/// <summary>Marker class for ILogger category specific to NuGetTools.</summary>
public class NuGetToolLogger { } // Made public

[McpServerToolType] // Corrected attribute name based on sample
public static class NuGetTools
{
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
            // Consider how to report errors via MCP - for now, return empty or let exception propagate if MCP SDK handles it
            // Changed Mcp.AnalyzedDependency to just AnalyzedDependency (using updated namespace)
            return Enumerable.Empty<AnalyzedDependency>();
        }
    }

    [McpServerTool] // Corrected attribute name based on sample
    [Description("Searches the configured NuGet feed for packages matching a given search term.")]
    // Changed Dtos.PackageSearchResult to just PackageSearchResult (using updated namespace)
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
             // Changed Dtos.PackageSearchResult to just PackageSearchResult (using updated namespace)
             return Enumerable.Empty<PackageSearchResult>();
         }
     }
 
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
             return Enumerable.Empty<string>();
        }
    }

    [McpServerTool] // Corrected attribute name based on sample
    [Description("Gets the latest version (stable or including pre-release) for a specific NuGet package ID from the configured feed.")]
    // Changed Mcp.PackageVersionInfo to just PackageVersionInfo (using updated namespace)
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
             return null; // Return null on error
        }
    }
}