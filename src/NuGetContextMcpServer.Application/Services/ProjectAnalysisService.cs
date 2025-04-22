using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Abstractions.Dtos; // Updated namespace
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetContextMcpServer.Application.Services;

public class ProjectAnalysisService : IProjectAnalysisService // Interface now in Abstractions
{
    private readonly ISolutionParser _solutionParser; // Interface now in Abstractions
    private readonly IProjectParser _projectParser; // Interface now in Abstractions
    private readonly INuGetQueryService _nugetQueryService; // Interface now in Abstractions
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
                // Let the parser handle if the file doesn't exist
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

            // Process projects (consider parallelizing if many projects)
            var allDependencies = new List<AnalyzedDependency>();
            foreach (var projectPath in projectPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogDebug("Analyzing project: {ProjectPath}", projectPath);
                try // Add try-catch around project parsing
                {
                    var projectReferences = await _projectParser.GetPackageReferencesAsync(projectPath, cancellationToken);

                    foreach (var projRef in projectReferences)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _logger.LogDebug("Fetching latest versions for package: {PackageId}", projRef.Id);

                        try // Add try-catch around NuGet queries for a single dependency
                        {
                            // Fetch latest stable and absolute latest versions
                            var latestStableTask = _nugetQueryService.GetLatestStableVersionAsync(projRef.Id, cancellationToken);
                            var latestAbsoluteTask = _nugetQueryService.GetLatestVersionAsync(projRef.Id, cancellationToken); // Includes prerelease

                            await Task.WhenAll(latestStableTask, latestAbsoluteTask);

                            var latestStable = await latestStableTask;
                            var latestAbsolute = await latestAbsoluteTask;

                            allDependencies.Add(new AnalyzedDependency(
                                projRef.Id,
                                projRef.Version, // Assuming ProjectReference has a Version property
                                latestStable?.ToNormalizedString(),
                                latestAbsolute?.ToNormalizedString()
                            ));
                        }
                        catch (Exception nugetEx) when (nugetEx is not OperationCanceledException)
                        {
                             _logger.LogWarning(nugetEx, "Failed to get latest versions for package {PackageId} from project {ProjectPath}", projRef.Id, projectPath);
                             // Optionally add a partial result or skip
                        }
                    }
                }
                catch (Exception parseEx) when (parseEx is not OperationCanceledException)
                {
                     _logger.LogError(parseEx, "Failed to parse project {ProjectPath}", projectPath);
                     // Continue to the next project
                }
            }

            // Deduplicate dependencies based on PackageId, taking the first one encountered (adjust logic if needed, e.g., highest version)
            var uniqueDependencies = allDependencies
                .GroupBy(d => d.Id)
                .Select(g => g.First()) // Or use MaxBy(d => VersionRange.Parse(d.RequestedVersion ?? "0.0.0")) etc.
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