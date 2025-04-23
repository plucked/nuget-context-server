using Microsoft.Build.Construction;
using Microsoft.Extensions.Logging;
using NuGetContextMcpServer.Abstractions.Interfaces;
// Updated namespace

namespace NuGetContextMcpServer.Infrastructure.Parsing;

public class MsBuildSolutionParser : ISolutionParser
{
    private readonly ILogger<MsBuildSolutionParser> _logger;

    public MsBuildSolutionParser(ILogger<MsBuildSolutionParser> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<string>> GetProjectPathsAsync(string solutionPath, CancellationToken cancellationToken)
    {
        // Ensure MSBuild is registered (should have happened at startup)
        // MsBuildInitializer.EnsureMsBuildRegistered(); // Already called in Program.cs

        return Task.Run(() => // Run potentially blocking file I/O and parsing off the main thread if needed
        {
            List<string> projectPaths = [];
            try
            {
                if (!File.Exists(solutionPath))
                {
                    _logger.LogError("Solution file not found at {Path}", solutionPath);
                    return [];
                }

                _logger.LogDebug("Parsing solution file: {Path}", solutionPath);
                var solutionFile = SolutionFile.Parse(solutionPath);

                foreach (var projectInSolution in solutionFile.ProjectsInOrder)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Filter out solution folders, non-MSBuild projects, etc.
                    if (projectInSolution.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat &&
                        !string.IsNullOrEmpty(projectInSolution.AbsolutePath)) // Ensure path exists
                    {
                        // Check if the project file actually exists
                        if (File.Exists(projectInSolution.AbsolutePath))
                        {
                            projectPaths.Add(projectInSolution.AbsolutePath);
                            _logger.LogDebug("Found project {ProjectName} at {Path}", projectInSolution.ProjectName,
                                projectInSolution.AbsolutePath);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Project '{ProjectName}' listed in solution but not found at expected path: {Path}",
                                projectInSolution.ProjectName, projectInSolution.AbsolutePath);
                        }
                    }
                }

                _logger.LogInformation("Parsed {Count} valid project paths from solution {Path}", projectPaths.Count,
                    solutionPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing solution file {Path}", solutionPath);
                // Depending on requirements, might rethrow or return empty/partial list
                return [];
            }

            return projectPaths.AsEnumerable();
        }, cancellationToken);
    }
}