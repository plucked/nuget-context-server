namespace NuGetContextMcpServer.Abstractions.Interfaces;

/// <summary>
///     Represents a parsed NuGet package reference with its identifier and version.
/// </summary>
/// <param name="Id">The package identifier.</param>
/// <param name="Version">The package version.</param>
public record ParsedPackageReference(string Id, string Version);

/// <summary>
///     Defines an interface for parsing project files to extract information,
///     specifically NuGet package references.
/// </summary>
public interface IProjectParser
{
    /// <summary>
    ///     Asynchronously retrieves the NuGet package references from a specified project file.
    /// </summary>
    /// <param name="projectPath">The full path to the project file.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <param name="globalProperties">Optional global MSBuild properties to use during parsing.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains
    ///     an enumerable collection of <see cref="ParsedPackageReference" /> objects.
    /// </returns>
    Task<IEnumerable<ParsedPackageReference>> GetPackageReferencesAsync(string projectPath,
        CancellationToken cancellationToken, IDictionary<string, string>? globalProperties = null);
}