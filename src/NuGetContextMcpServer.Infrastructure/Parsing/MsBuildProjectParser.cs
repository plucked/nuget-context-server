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
    private readonly ProjectCollection _projectCollection;

    public MsBuildProjectParser(ILogger<MsBuildProjectParser> logger)
    {
        _logger = logger;
        // Using GlobalProjectCollection is simpler but loads projects into a shared space.
        // A dedicated ProjectCollection might offer better isolation if needed.
        _projectCollection = ProjectCollection.GlobalProjectCollection;
    }

    public Task<IEnumerable<ParsedPackageReference>> GetPackageReferencesAsync(string projectPath, CancellationToken cancellationToken)
    {
        // Ensure MSBuild is registered (should have happened at startup)
        // MsBuildInitializer.EnsureMsBuildRegistered(); // Already called in Program.cs

        return Task.Run(() => // Run potentially blocking evaluation off the main thread
        {
            List<ParsedPackageReference> references = new();
            Project? project = null;
            try
            {
                if (!File.Exists(projectPath))
                {
                    _logger.LogError("Project file not found at {Path}", projectPath);
                    return Enumerable.Empty<ParsedPackageReference>();
                }

                _logger.LogDebug("Loading and evaluating project: {Path}", projectPath);

                // Load and evaluate the project. Pass null for global properties initially.
                // Consider if specific configurations (Debug/Release) are needed.
                project = _projectCollection.LoadProject(projectPath, null, null);

                // Extract PackageReferences using the evaluated items
                var packageReferenceItems = project.GetItems("PackageReference");

                foreach (var item in packageReferenceItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string packageId = item.EvaluatedInclude;
                    // Use GetMetadataValue which respects evaluation (conditions, CPM etc.)
                    string packageVersion = item.GetMetadataValue("Version");

                    if (!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(packageVersion))
                    {
                        references.Add(new ParsedPackageReference(packageId, packageVersion));
                        _logger.LogDebug("Found PackageReference: {Id} Version: {Version}", packageId, packageVersion);
                    }
                    else
                    {
                         _logger.LogWarning("Found PackageReference with missing ID or Version in project {Path}: Include='{Include}'", projectPath, packageId);
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
                // If using a dedicated ProjectCollection, unload the project
                // if (project != null && _projectCollection != ProjectCollection.GlobalProjectCollection)
                // {
                //     _projectCollection.UnloadProject(project);
                //     _logger.LogDebug("Unloaded project: {Path}", projectPath);
                // }
            }
            return references.AsEnumerable();
        }, cancellationToken);
    }
}