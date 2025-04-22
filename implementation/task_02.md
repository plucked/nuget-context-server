# Task 02: Interfaces, Configuration, and MSBuild Initialization

**Goal:** Define the core service interfaces, set up basic configuration handling, and implement the MSBuild environment initialization logic.

**Outcome:** Service interfaces are defined in the `Application` project, configuration (`appsettings.json`, User Secrets, Options pattern) is set up in the `Host` project, and the `MsBuildInitializer` is implemented in the `Infrastructure` project and called correctly from the `Host`.

---

## Sub-Tasks:

### 2.1 Define Service Interfaces (`Application` Project)
*   **Action:** Create an `Interfaces` folder within the `NuGetContextMcpServer.Application` project. Define the following C# interfaces within this folder:
    *   `IProjectParser.cs`: Interface for parsing `.csproj` files.
        ```csharp
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        namespace NuGetContextMcpServer.Application.Interfaces;

        // Represents the result of parsing a package reference
        public record ParsedPackageReference(string Id, string Version);

        public interface IProjectParser
        {
            Task<IEnumerable<ParsedPackageReference>> GetPackageReferencesAsync(string projectPath, CancellationToken cancellationToken);
            // Potentially add methods for other project details if needed later
        }
        ```
    *   `ISolutionParser.cs`: Interface for parsing `.sln` files.
        ```csharp
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        namespace NuGetContextMcpServer.Application.Interfaces;

        public interface ISolutionParser
        {
            Task<IEnumerable<string>> GetProjectPathsAsync(string solutionPath, CancellationToken cancellationToken);
        }
        ```
    *   `INuGetQueryService.cs`: Interface for interacting with NuGet feeds.
        ```csharp
        using NuGet.Versioning;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using NuGetContextMcpServer.Infrastructure.Mcp; // Assuming DTOs defined here or shared location

        namespace NuGetContextMcpServer.Application.Interfaces;

        public interface INuGetQueryService
        {
            Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease, CancellationToken cancellationToken);
            Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string packageId, CancellationToken cancellationToken);
            Task<NuGetVersion?> GetLatestStableVersionAsync(string packageId, CancellationToken cancellationToken);
            Task<NuGetVersion?> GetLatestVersionAsync(string packageId, CancellationToken cancellationToken); // Includes prerelease
            // Potentially add GetPackageMetadataAsync if needed later
        }
        ```
    *   `ICacheService.cs`: Interface for the caching layer.
        ```csharp
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        namespace NuGetContextMcpServer.Application.Interfaces;

        public interface ICacheService
        {
            Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class;
            Task SetAsync<T>(string key, T value, TimeSpan absoluteExpirationRelativeToNow, CancellationToken cancellationToken) where T : class;
            Task RemoveAsync(string key, CancellationToken cancellationToken);
            Task RemoveExpiredAsync(CancellationToken cancellationToken); // For eviction service
        }
        ```
    *   `IProjectAnalysisService.cs`: Interface for the main analysis logic.
        ```csharp
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using NuGetContextMcpServer.Infrastructure.Mcp; // Assuming DTOs defined here or shared location

        namespace NuGetContextMcpServer.Application.Interfaces;

        public interface IProjectAnalysisService
        {
            Task<IEnumerable<AnalyzedDependency>> AnalyzeProjectAsync(string projectOrSolutionPath, CancellationToken cancellationToken);
        }
        ```
    *   `IPackageSearchService.cs`: Interface for package search logic.
        ```csharp
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using NuGetContextMcpServer.Infrastructure.Mcp; // Assuming DTOs defined here or shared location

        namespace NuGetContextMcpServer.Application.Interfaces;

        public interface IPackageSearchService
        {
             Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease, CancellationToken cancellationToken);
        }
        ```
    *   `IPackageVersionService.cs`: Interface for package version logic.
        ```csharp
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using NuGetContextMcpServer.Infrastructure.Mcp; // Assuming DTOs defined here or shared location

        namespace NuGetContextMcpServer.Application.Interfaces;

        public interface IPackageVersionService
        {
            Task<IEnumerable<string>> GetPackageVersionsAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken);
            Task<PackageVersionInfo?> GetLatestPackageVersionAsync(string packageId, bool includePrerelease, CancellationToken cancellationToken);
        }
        ```
*   **Outcome:** All specified interfaces exist in `src/NuGetContextMcpServer.Application/Interfaces/`.

### 2.2 Configure `appsettings.json` (`Host` Project)
*   **Action:** Create/Update `appsettings.json` and `appsettings.Development.json` in the `NuGetContextMcpServer.Host` project. Define the `NuGetSettings` and `CacheSettings` sections.
*   **File Content (`appsettings.json`):**
    ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.Hosting.Lifetime": "Information",
          "ModelContextProtocol": "Information" // Adjust MCP logging level as needed
        }
      },
      "NuGetSettings": {
        "QueryFeedUrl": "https://api.nuget.org/v3/index.json"
        // Username and PasswordOrPat will come from User Secrets or Env Vars
      },
      "CacheSettings": {
        "DatabasePath": "nuget_cache.db", // Relative path for SQLite DB file
        "DefaultExpirationMinutes": 60
      }
    }
    ```
*   **File Content (`appsettings.Development.json`):**
    ```json
    {
      "Logging": {
        "LogLevel": {
          "Default": "Debug",
          "Microsoft.Hosting.Lifetime": "Information",
          "ModelContextProtocol": "Debug" // More verbose logging in dev
        }
      }
      // Development-specific overrides if needed
    }
    ```
*   **Outcome:** Configuration files exist with the specified structure. Ensure these files are copied to the output directory on build (check `.csproj` properties: `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`).

### 2.3 Set Up User Secrets (`Host` Project)
*   **Action:** Initialize User Secrets for the `Host` project and add placeholders for NuGet credentials.
*   **Command (Example):**
    ```bash
    cd src/NuGetContextMcpServer.Host
    dotnet user-secrets init
    dotnet user-secrets set "NuGetSettings:Username" "YOUR_PRIVATE_FEED_USERNAME_OR_PLACEHOLDER"
    dotnet user-secrets set "NuGetSettings:PasswordOrPat" "YOUR_PRIVATE_FEED_PAT_OR_PLACEHOLDER"
    cd ../..
    ```
*   **Outcome:** User Secrets are initialized, and placeholder keys for credentials exist. Developers need to update these locally if accessing private feeds.

### 2.4 Define Configuration POCOs (`Infrastructure` or `Application`)
*   **Action:** Create POCO classes to represent the configuration sections. These can live in `Infrastructure` or a shared `Common` project if one is created later. For now, let's place them in `Infrastructure`. Create a `Configuration` folder.
    *   `NuGetSettings.cs`:
        ```csharp
        namespace NuGetContextMcpServer.Infrastructure.Configuration;

        public class NuGetSettings
        {
            public string QueryFeedUrl { get; set; } = string.Empty;
            public string? Username { get; set; }
            public string? PasswordOrPat { get; set; }
        }
        ```
    *   `CacheSettings.cs`:
        ```csharp
        namespace NuGetContextMcpServer.Infrastructure.Configuration;

        public class CacheSettings
        {
            public string DatabasePath { get; set; } = "nuget_cache.db";
            public int DefaultExpirationMinutes { get; set; } = 60;
        }
        ```
*   **Outcome:** `NuGetSettings.cs` and `CacheSettings.cs` exist in `src/NuGetContextMcpServer.Infrastructure/Configuration/`.

### 2.5 Implement `MsBuildInitializer` (`Infrastructure` Project)
*   **Action:** Create a `Parsing` folder in the `Infrastructure` project. Implement the `MsBuildInitializer.cs` class as defined in the research documents.
*   **File Content (`MsBuildInitializer.cs`):**
    ```csharp
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
    ```
*   **Outcome:** `MsBuildInitializer.cs` exists in `src/NuGetContextMcpServer.Infrastructure/Parsing/`.

### 2.6 Configure Host (`Host` Project - `Program.cs`)
*   **Action:** Update `Program.cs` in the `Host` project to:
    1.  Call `MsBuildInitializer.EnsureMsBuildRegistered()` *before* `Host.CreateApplicationBuilder`.
    2.  Configure logging.
    3.  Register the configuration options (`NuGetSettings`, `CacheSettings`).
*   **File Content (`Program.cs` - Initial Setup):**
    ```csharp
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NuGetContextMcpServer.Infrastructure.Parsing; // For MsBuildInitializer
    using NuGetContextMcpServer.Infrastructure.Configuration; // For Settings classes

    // --- MSBuild Locator Registration ---
    // Create a temporary logger factory for the initializer if needed
    using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    MsBuildInitializer.EnsureMsBuildRegistered(loggerFactory);
    // --- End MSBuild Locator Registration ---

    var builder = Host.CreateApplicationBuilder(args);

    // --- Configure Logging (Reads from appsettings.json by default) ---
    // builder.Logging.ClearProviders(); // Optional: Clear default providers if needed
    // builder.Logging.AddConsole();

    // --- Configure Options Pattern ---
    builder.Services.Configure<NuGetSettings>(builder.Configuration.GetSection("NuGetSettings"));
    builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection("CacheSettings"));

    // --- Configure Application Services (DI - Placeholder for now) ---
    // TODO: Register services in Task 03+
    // builder.Services.AddSingleton<IProjectParser, MsBuildProjectParser>();
    // ... other services ...

    // --- Configure MCP Server (Placeholder for now) ---
    // TODO: Configure MCP in Task 03+
    // builder.Services.AddMcpServer()
    //    .WithStdioServerTransport()
    //    .WithToolsFromAssembly(...);

    // --- Build and Run Host ---
    var host = builder.Build();

    var mainLogger = host.Services.GetRequiredService<ILogger<Program>>();
    mainLogger.LogInformation("Host built. Starting application...");

    await host.RunAsync();

    mainLogger.LogInformation("Application shutting down.");
    ```
*   **Outcome:** `Program.cs` correctly initializes MSBuild, configures logging based on `appsettings.json`, and sets up the Options pattern for `NuGetSettings` and `CacheSettings`. The application should build and run, although it doesn't do much yet.

---