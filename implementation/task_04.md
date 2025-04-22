# Task 04: Implement Application Services and MCP Tools

**Goal:** Implement the application service logic that coordinates infrastructure components and define the MCP tools that expose this logic.

**Outcome:** Functional implementations of `ProjectAnalysisService`, `PackageSearchService`, `PackageVersionService` exist in the `Application` project. MCP tool definitions (`NuGetTools.cs` with DTOs) exist in the `Infrastructure` project.

---

## Sub-Tasks:

### 4.1 Implement `PackageSearchService` (`Application` Project)
*   **Action:** Create a `Services` folder in the `Application` project. Implement `PackageSearchService` inheriting from `IPackageSearchService`.
*   **File Content (`PackageSearchService.cs`):**
    ```csharp
    using Microsoft.Extensions.Logging;
    using NuGetContextMcpServer.Application.Interfaces;
    using NuGetContextMcpServer.Infrastructure.Mcp; // Assuming DTOs defined here or shared location
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    namespace NuGetContextMcpServer.Application.Services;

    public class PackageSearchService : IPackageSearchService
    {
        private readonly INuGetQueryService _nugetQueryService;
        private readonly ILogger<PackageSearchService> _logger;

        public PackageSearchService(INuGetQueryService nugetQueryService, ILogger<PackageSearchService> logger)
        {
            _nugetQueryService = nugetQueryService;
            _logger = logger;
        }

        public async Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Searching packages for term: {SearchTerm}, IncludePrerelease: {IncludePrerelease}", searchTerm, includePrerelease);
            try
            {
                // Directly delegate to the infrastructure service
                var results = await _nugetQueryService.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken);
                _logger.LogInformation("Found {Count} packages for term: {SearchTerm}", results.Count(), searchTerm);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching packages for term: {SearchTerm}", searchTerm);
                // Depending on desired error handling, could return empty or rethrow
                return Enumerable.Empty<PackageSearchResult>();
            }
        }
    }
    ```
*   **Outcome:** `PackageSearchService.cs` implemented.

### 4.2 Implement `PackageVersionService` (`Application` Project)
*   **Action:** Implement `PackageVersionService` in the `Services` folder, inheriting from `IPackageVersionService`.
*   **File Content (`PackageVersionService.cs`):**
    ```csharp
    using Microsoft.Extensions.Logging;
    using NuGet.Versioning;
    using NuGetContextMcpServer.Application.Interfaces;
    using NuGetContextMcpServer.Infrastructure.Mcp; // Assuming DTOs defined here or shared location
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    namespace NuGetContextMcpServer.Application.Services;

    public class PackageVersionService : IPackageVersionService
    {
        private readonly INuGetQueryService _nugetQueryService;
        private readonly ILogger<PackageVersionService> _logger;

        public PackageVersionService(INuGetQueryService nugetQueryService, ILogger<PackageVersionService> logger)
        {
            _nugetQueryService = nugetQueryService;
            _logger = logger;
        }

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

                var versionStrings = versions.Select(v => v.ToNormalizedString()).OrderByDescending(v => NuGetVersion.Parse(v)).ToList(); // Order descending
                _logger.LogInformation("Found {Count} versions for package: {PackageId}", versionStrings.Count, packageId);
                return versionStrings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting versions for package: {PackageId}", packageId);
                return Enumerable.Empty<string>();
            }
        }

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
    ```
*   **Outcome:** `PackageVersionService.cs` implemented.

### 4.3 Implement `ProjectAnalysisService` (`Application` Project)
*   **Action:** Implement `ProjectAnalysisService` in the `Services` folder, inheriting from `IProjectAnalysisService`. This service coordinates parsing and NuGet queries.
*   **File Content (`ProjectAnalysisService.cs`):**
    ```csharp
    using Microsoft.Extensions.Logging;
    using NuGet.Versioning;
    using NuGetContextMcpServer.Application.Interfaces;
    using NuGetContextMcpServer.Infrastructure.Mcp; // Assuming DTOs defined here or shared location
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    namespace NuGetContextMcpServer.Application.Services;

    public class ProjectAnalysisService : IProjectAnalysisService
    {
        private readonly ISolutionParser _solutionParser;
        private readonly IProjectParser _projectParser;
        private readonly INuGetQueryService _nugetQueryService;
        private readonly ILogger<ProjectAnalysisService> _logger;

        public ProjectAnalysisService(
            ISolutionParser solutionParser,
            IProjectParser projectParser,
            INuGetQueryService nugetQueryService,
            ILogger<ProjectAnalysisService> logger)
        {
            _solutionParser = solutionParser;
            _projectParser = projectParser;
            _nugetQueryService = nugetQueryService;
            _logger = logger;
        }

        public async Task<IEnumerable<AnalyzedDependency>> AnalyzeProjectAsync(string projectOrSolutionPath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting analysis for path: {Path}", projectOrSolutionPath);
            List<string> projectPaths = new();

            try
            {
                if (projectOrSolutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    var paths = await _solutionParser.GetProjectPathsAsync(projectOrSolutionPath, cancellationToken);
                    projectPaths.AddRange(paths);
                }
                else if (projectOrSolutionPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) // Add other project types if needed (.vbproj, .fsproj)
                {
                    if (File.Exists(projectOrSolutionPath))
                    {
                        projectPaths.Add(projectOrSolutionPath);
                    }
                    else
                    {
                         _logger.LogError("Project file not found at path: {Path}", projectOrSolutionPath);
                         return Enumerable.Empty<AnalyzedDependency>();
                    }
                }
                else
                {
                    _logger.LogError("Invalid file type provided. Path must end with .sln or .csproj: {Path}", projectOrSolutionPath);
                    return Enumerable.Empty<AnalyzedDependency>();
                }

                if (!projectPaths.Any())
                {
                    _logger.LogWarning("No valid projects found to analyze for path: {Path}", projectOrSolutionPath);
                    return Enumerable.Empty<AnalyzedDependency>();
                }

                // Process projects (consider parallelizing if many projects)
                var allDependencies = new List<AnalyzedDependency>();
                foreach (var projectPath in projectPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _logger.LogDebug("Analyzing project: {ProjectPath}", projectPath);
                    var projectReferences = await _projectParser.GetPackageReferencesAsync(projectPath, cancellationToken);

                    foreach (var projRef in projectReferences)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _logger.LogDebug("Fetching latest versions for package: {PackageId}", projRef.Id);

                        // Fetch latest stable and absolute latest versions
                        var latestStableTask = _nugetQueryService.GetLatestStableVersionAsync(projRef.Id, cancellationToken);
                        var latestAbsoluteTask = _nugetQueryService.GetLatestVersionAsync(projRef.Id, cancellationToken); // Includes prerelease

                        await Task.WhenAll(latestStableTask, latestAbsoluteTask);

                        var latestStable = await latestStableTask;
                        var latestAbsolute = await latestAbsoluteTask;

                        allDependencies.Add(new AnalyzedDependency(
                            projRef.Id,
                            projRef.Version,
                            latestStable?.ToNormalizedString(),
                            latestAbsolute?.ToNormalizedString()
                        ));
                    }
                }

                // Optional: Deduplicate if the same package/version appears in multiple projects?
                // For now, return all found references.
                _logger.LogInformation("Analysis complete for path: {Path}. Found {Count} total dependencies.", projectOrSolutionPath, allDependencies.Count);
                return allDependencies;

            }
            catch (OperationCanceledException)
            {
                 _logger.LogWarning("Analysis cancelled for path: {Path}", projectOrSolutionPath);
                 throw; // Rethrow cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during analysis for path: {Path}", projectOrSolutionPath);
                return Enumerable.Empty<AnalyzedDependency>(); // Return empty on error
            }
        }
    }
    ```
*   **Outcome:** `ProjectAnalysisService.cs` implemented.

### 4.4 Define MCP Tool DTOs (`Infrastructure` Project)
*   **Action:** Create an `Mcp` folder in the `Infrastructure` project. Define the records/DTOs used as return types for the MCP tools.
*   **File Content (`McpDtos.cs` - Example, adjust namespace if needed):**
    ```csharp
    using System.Collections.Generic;

    namespace NuGetContextMcpServer.Infrastructure.Mcp;

    // DTO for AnalyzeProjectDependenciesAsync tool
    public record AnalyzedDependency(
        string Id,
        string RequestedVersion, // Version specified in the project file
        string? LatestStableVersion,
        string? LatestVersion // Including prerelease
    );

    // DTO for SearchNuGetPackagesAsync tool
    public record PackageSearchResult(
        string Id,
        string Version,
        string Description,
        string? ProjectUrl
    );

    // DTO for GetLatestNuGetPackageVersionAsync tool
    public record PackageVersionInfo(
        string PackageId,
        string LatestVersion
    );

    // Note: GetNuGetPackageVersionsAsync returns IEnumerable<string>, no specific DTO needed.
    ```
*   **Outcome:** DTO records for MCP tool outputs are defined.

### 4.5 Implement MCP Tools (`Infrastructure` Project - `Mcp` Folder)
*   **Action:** Implement the `NuGetTools.cs` static class with methods decorated for MCP. Inject application services and delegate calls.
*   **File Content (`NuGetTools.cs`):**
    ```csharp
    using ModelContextProtocol.Attributes;
    using System.ComponentModel;
    using Microsoft.Extensions.Logging;
    using NuGetContextMcpServer.Application.Interfaces;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    namespace NuGetContextMcpServer.Infrastructure.Mcp; // Ensure DTOs are accessible

    [McpTools] // Mark class as containing tools
    public static class NuGetTools
    {
        [McpTool]
        [Description("Analyzes a specified .NET solution (.sln) or project (.csproj) file to find its NuGet package dependencies and their latest available versions on the configured feed.")]
        public static async Task<IEnumerable<AnalyzedDependency>> AnalyzeProjectDependenciesAsync(
            [Description("The absolute path to the .sln or .csproj file accessible by the server.")] string projectOrSolutionPath,
            IProjectAnalysisService analysisService, // Injected via DI
            ILogger<NuGetTools> logger, // Injected via DI
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
                return Enumerable.Empty<AnalyzedDependency>();
            }
        }

        [McpTool]
        [Description("Searches the configured NuGet feed for packages matching a given search term.")]
        public static async Task<IEnumerable<PackageSearchResult>> SearchNuGetPackagesAsync(
            [Description("The term to search for.")] string searchTerm,
            [Description("Whether to include pre-release package versions in the search results (default: false).")] bool includePrerelease,
            IPackageSearchService searchService, // Injected via DI
            ILogger<NuGetTools> logger, // Injected via DI
            CancellationToken cancellationToken)
        {
             logger.LogDebug("MCP Tool 'SearchNuGetPackagesAsync' invoked for term: {SearchTerm}", searchTerm);
             try
             {
                return await searchService.SearchPackagesAsync(searchTerm, includePrerelease, cancellationToken);
             }
             catch (Exception ex)
             {
                 logger.LogError(ex, "Error executing MCP Tool 'SearchNuGetPackagesAsync' for term: {SearchTerm}", searchTerm);
                 return Enumerable.Empty<PackageSearchResult>();
             }
        }

        [McpTool]
        [Description("Lists all available versions for a specific NuGet package ID from the configured feed.")]
        public static async Task<IEnumerable<string>> GetNuGetPackageVersionsAsync(
            [Description("The exact ID of the NuGet package.")] string packageId,
            [Description("Whether to include pre-release package versions (default: false).")] bool includePrerelease,
            IPackageVersionService versionService, // Injected via DI
            ILogger<NuGetTools> logger, // Injected via DI
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

        [McpTool]
        [Description("Gets the latest version (stable or including pre-release) for a specific NuGet package ID from the configured feed.")]
        public static async Task<PackageVersionInfo?> GetLatestNuGetPackageVersionAsync(
             [Description("The exact ID of the NuGet package.")] string packageId,
             [Description("If true, returns the absolute latest version (including pre-release); otherwise, returns the latest stable version (default: false).")] bool includePrerelease,
             IPackageVersionService versionService, // Injected via DI
             ILogger<NuGetTools> logger, // Injected via DI
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
    ```
*   **Outcome:** `NuGetTools.cs` implemented with static methods decorated for MCP, injecting and delegating to application services.

---