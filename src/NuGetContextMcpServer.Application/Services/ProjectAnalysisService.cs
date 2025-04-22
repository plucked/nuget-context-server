using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using NuGetContextMcpServer.Abstractions.Interfaces;
using NuGetContextMcpServer.Abstractions.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetContextMcpServer.Application.Services;

/// <summary>
/// Provides functionality to analyze .NET projects or solutions to determine their NuGet package dependencies
/// and find the latest available versions for those dependencies.
/// </summary>
public class ProjectAnalysisService : IProjectAnalysisService
{
    private readonly ISolutionParser _solutionParser;
    private readonly IProjectParser _projectParser;
    private readonly INuGetQueryService _nugetQueryService;
    private readonly ILogger<ProjectAnalysisService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectAnalysisService"/> class.
    /// </summary>
    /// <param name="solutionParser">The parser for solution files (.sln).</param>
    /// <param name="projectParser">The parser for project files (e.g., .csproj).</param>
    /// <param name="nugetQueryService">The service used to query the NuGet feed for package versions.</param>
    /// <param name="logger">The logger for logging information and errors.</param>
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

    /// <summary>
    /// Analyzes a given .NET project or solution file asynchronously to identify NuGet package dependencies
    /// and retrieve their requested, latest stable, and latest absolute versions.
    /// </summary>
    /// <param name="projectOrSolutionPath">The full path to the .csproj or .sln file to analyze.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains an enumerable collection
    /// of unique <see cref="AnalyzedDependency"/> objects, detailing each dependency found.
    /// Returns an empty collection if the path is invalid, no projects are found, or an unrecoverable error occurs.
    /// </returns>
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
            else if (projectOrSolutionPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) // Consider adding other project types like .vbproj, .fsproj if needed
            {
                projectPaths.Add(projectOrSolutionPath);
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

            var allDependencies = new List<AnalyzedDependency>();
            // Consider parallelizing project processing if performance becomes an issue for large solutions
            foreach (var projectPath in projectPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogDebug("Analyzing project: {ProjectPath}", projectPath);
                try
                {
                    var projectReferences = await _projectParser.GetPackageReferencesAsync(projectPath, cancellationToken);

                    foreach (var projRef in projectReferences)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _logger.LogDebug("Fetching latest versions for package: {PackageId}", projRef.Id);

                        try
                        {
                            // Fetch latest stable and absolute latest versions concurrently
                            var latestStableTask = _nugetQueryService.GetLatestStableVersionAsync(projRef.Id, cancellationToken);
                            var latestAbsoluteTask = _nugetQueryService.GetLatestVersionAsync(projRef.Id, cancellationToken); // Includes prerelease

                            await Task.WhenAll(latestStableTask, latestAbsoluteTask);

                            var latestStable = await latestStableTask;
                            var latestAbsolute = await latestAbsoluteTask;

                            allDependencies.Add(new AnalyzedDependency(
                                projRef.Id,
                                projRef.Version ?? "0.0.0", // Use default if version is null
                                latestStable?.ToNormalizedString(),
                                latestAbsolute?.ToNormalizedString()
                            ));
                        }
                        catch (Exception nugetEx) when (nugetEx is not OperationCanceledException)
                        {
                             _logger.LogWarning(nugetEx, "Failed to get latest versions for package {PackageId} from project {ProjectPath}. Skipping dependency.", projRef.Id, projectPath);
                             // Skip adding this dependency if NuGet query fails
                        }
                    }
                }
                catch (Exception parseEx) when (parseEx is not OperationCanceledException)
                {
                     _logger.LogError(parseEx, "Failed to parse project {ProjectPath}. Skipping project.", projectPath);
                     // Continue to the next project
                }
            }

            // Deduplicate dependencies based on PackageId.
            // Currently takes the first one encountered if a package is referenced in multiple projects.
            // A different strategy (e.g., highest requested version) might be more appropriate depending on requirements.
            var uniqueDependencies = allDependencies
                .GroupBy(d => d.Id)
                .Select(g => g.First()) // Simple deduplication: take the first instance found.
                .ToList();

            _logger.LogInformation("Analysis complete for path: {Path}. Found {Count} unique dependencies.", projectOrSolutionPath, uniqueDependencies.Count);
            return uniqueDependencies;

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