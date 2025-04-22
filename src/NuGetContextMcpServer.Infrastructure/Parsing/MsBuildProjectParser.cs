using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetContextMcpServer.Infrastructure.Parsing;

public class MsBuildProjectParser : IProjectParser
{
    private readonly ILogger<MsBuildProjectParser> _logger;
    // Consider ProjectCollection lifetime management if not using GlobalProjectCollection
    // Using a shared ProjectCollection can sometimes lead to evaluation issues or state conflicts.
    // Switched to creating a new one per call for better isolation, especially for CPM tests.
    // private readonly ProjectCollection _projectCollection;

    public MsBuildProjectParser(ILogger<MsBuildProjectParser> logger)
    {
        _logger = logger;
        // Using GlobalProjectCollection is simpler but loads projects into a shared space.
        // A dedicated ProjectCollection might offer better isolation if needed.
        // _projectCollection = ProjectCollection.GlobalProjectCollection; // Replaced with per-call instance
    }

    public Task<IEnumerable<ParsedPackageReference>> GetPackageReferencesAsync(string projectPath, CancellationToken cancellationToken, IDictionary<string, string>? globalProperties = null)
    {
        // Ensure MSBuild is registered (should have happened at startup)
        // MsBuildInitializer.EnsureMsBuildRegistered(); // Already called in Program.cs

        return Task.Run(() => // Run potentially blocking evaluation off the main thread
        {
            List<ParsedPackageReference> references = new();
            Project? project = null;
            ProjectCollection? projectCollection = null; // Use a new collection for isolation
            try
            {
                if (!File.Exists(projectPath))
                {
                    _logger.LogError("Project file not found at {Path}", projectPath);
                    return Enumerable.Empty<ParsedPackageReference>();
                }

                _logger.LogDebug("Loading and evaluating project: {Path} with GlobalProperties: {Properties}", projectPath, globalProperties);

                // Use a new ProjectCollection for better isolation
                projectCollection = new ProjectCollection();
                _logger.LogDebug("Created new ProjectCollection for parsing {Path}", projectPath);

                // Load and evaluate the project using the new collection
                project = projectCollection.LoadProject(projectPath, globalProperties, null);

                // Extract PackageReferences using the evaluated items
                // Get both PackageReference and PackageVersion items after evaluation
                var packageReferenceItems = project.GetItems("PackageReference");
                var packageVersionItems = project.GetItems("PackageVersion"); // Get centrally defined versions

                // Create a lookup for CPM versions
                var cpmVersions = packageVersionItems
                    .ToDictionary(pv => pv.EvaluatedInclude, pv => pv.GetMetadataValue("Version"), StringComparer.OrdinalIgnoreCase);
                _logger.LogDebug("Found {Count} PackageVersion items from Directory.Packages.props.", cpmVersions.Count);

                foreach (var item in packageReferenceItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string packageId = item.EvaluatedInclude;
                    // Use GetMetadataValue which respects evaluation (conditions, CPM etc.)
                    // Try getting version directly from PackageReference metadata first
                    string packageVersion = item.GetMetadataValue("Version");

                    // If version is missing (common with CPM), try looking it up in the PackageVersion items
                    if (string.IsNullOrEmpty(packageVersion) && cpmVersions.TryGetValue(packageId, out var cpmVersion))
                    {
                        packageVersion = cpmVersion;
                        _logger.LogDebug("Resolved version '{Version}' for package '{Id}' from PackageVersion items (CPM).", packageVersion, packageId);
                    }

                    if (!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(packageVersion))
                    {
                        references.Add(new ParsedPackageReference(packageId, packageVersion));
                        _logger.LogDebug("Found PackageReference: {Id} Version: {Version}", packageId, packageVersion);
                    }
                    else
                    {
                        _logger.LogWarning("Found PackageReference with missing ID or Version in project {Path}: Include='{Include}'", projectPath, packageId);
                        // Log available metadata for diagnostics, especially for CPM issues
                        var metadataString = string.Join(", ", item.Metadata.Select(m => $"{m.Name}={m.EvaluatedValue}"));
                        // _logger.LogDebug("Metadata for item '{Include}': {Metadata}", packageId, metadataString); // Keep this commented unless needed again
                    }
                }
                _logger.LogInformation("Parsed {Count} PackageReferences from project {Path}", references.Count, projectPath);
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException ex)
            {
                _logger.LogError(ex, "Invalid project file format for {Path}: {Message}", projectPath, ex.BaseMessage);
                return Enumerable.Empty<ParsedPackageReference>(); // Or throw specific exception
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing project file {Path}", projectPath);
                return Enumerable.Empty<ParsedPackageReference>(); // Or throw
            }
            finally
            {
                // Unload the project from the dedicated collection
                if (project != null && projectCollection != null)
                {
                    projectCollection.UnloadProject(project);
                    _logger.LogDebug("Unloaded project from dedicated ProjectCollection: {Path}", projectPath);
                }
            }
            return references.AsEnumerable();
        }, cancellationToken);
    }
}