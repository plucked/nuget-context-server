using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging; // Add logger for better diagnostics
using System;
using System.Linq;

namespace NuGetContextMcpServer.Infrastructure.Parsing;

public static class MsBuildInitializer
{
    private static bool _isMsBuildRegistered = false;
    private static readonly object _lock = new object();
    private static ILogger? _logger; // Optional: Allow logger injection

    // Call this method VERY early in Program.cs BEFORE Host.CreateApplicationBuilder
    public static void EnsureMsBuildRegistered(ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger(typeof(MsBuildInitializer));

        if (_isMsBuildRegistered)
        {
            _logger?.LogDebug("MSBuild already registered.");
            return;
        }

        lock (_lock)
        {
            if (_isMsBuildRegistered) return;

            try
            {
                // QueryVisualStudioInstances() includes .NET SDK installs
                var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
                _logger?.LogDebug("Found {Count} MSBuild instance(s).", instances.Count);

                // Select the latest version available. Handle case where none are found.
                // Consider adding more sophisticated selection logic if needed (e.g., specific version range)
                VisualStudioInstance? instance = instances.OrderByDescending(inst => inst.Version).FirstOrDefault();

                if (instance == null)
                {
                    _logger?.LogError("No MSBuild instance found. Project parsing will likely fail.");
                    // Depending on requirements, could throw an exception here.
                    // For now, log error and allow continuation, but parsing will fail later.
                    // throw new InvalidOperationException("MSBuild instance could not be found. Ensure .NET SDK or Visual Studio with MSBuild is installed.");
                    return; // Exit registration attempt
                }

                _logger?.LogInformation("Registering MSBuild instance {Version} located at {Path}", instance.Version, instance.MSBuildPath);
                MSBuildLocator.RegisterInstance(instance);
                _isMsBuildRegistered = true;
                _logger?.LogInformation("MSBuild registration successful.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error registering MSBuild instance.");
                // Rethrow or handle as appropriate for the application startup sequence
                throw;
            }
        }
    }
}