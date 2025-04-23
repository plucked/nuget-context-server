using NuGetContextMcpServer.Abstractions.Dtos;

namespace NuGetContextMcpServer.Abstractions.Interfaces;

/// <summary>
///     Defines the interface for a service that analyzes projects and solutions to identify dependencies.
/// </summary>
public interface IProjectAnalysisService
{
    /// <summary>
    ///     Analyzes a project or solution file asynchronously to retrieve its dependencies.
    /// </summary>
    /// <param name="projectOrSolutionPath">The full path to the project or solution file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains an enumerable collection of
    ///     analyzed dependencies.
    /// </returns>
    Task<IEnumerable<AnalyzedDependency>> AnalyzeProjectAsync(string projectOrSolutionPath,
        CancellationToken cancellationToken);
}