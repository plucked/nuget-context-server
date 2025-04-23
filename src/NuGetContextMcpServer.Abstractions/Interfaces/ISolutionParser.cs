namespace NuGetContextMcpServer.Abstractions.Interfaces;

/// <summary>
///     Represents a parser for solution files.
/// </summary>
public interface ISolutionParser
{
    /// <summary>
    ///     Gets the paths of all projects within a solution file.
    /// </summary>
    /// <param name="solutionPath">The path to the solution file.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of project paths.</returns>
    Task<IEnumerable<string>> GetProjectPathsAsync(string solutionPath, CancellationToken cancellationToken);
}