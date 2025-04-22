# Task 05: Configure Host, DI, MCP Server, and Cache Eviction

**Goal:** Configure the main `Host` application (`Program.cs`) to register all services with Dependency Injection, set up the MCP server, and implement and register the background service for cache eviction.

**Outcome:** A fully configured `Program.cs` that correctly wires up all application components, enabling the MCP server to run and utilize the implemented services, including caching and background eviction.

---

## Sub-Tasks:

### 5.1 Implement Cache Eviction Service (`Infrastructure` or `Host`)
*   **Action:** Implement the `CacheEvictionService` as a background service using `IHostedService`. This service will periodically trigger the `RemoveExpiredAsync` method of `ICacheService`. Let's place it in the `Infrastructure` project's `Caching` folder for better organization.
*   **File Content (`CacheEvictionService.cs`):**
    ```csharp
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NuGetContextMcpServer.Application.Interfaces;
    using NuGetContextMcpServer.Infrastructure.Configuration; // For CacheSettings
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    namespace NuGetContextMcpServer.Infrastructure.Caching;

    public class CacheEvictionService : IHostedService, IDisposable
    {
        private readonly ILogger<CacheEvictionService> _logger;
        private readonly IServiceProvider _serviceProvider; // To scope ICacheService per execution
        private readonly CacheSettings _cacheSettings;
        private Timer? _timer;
        private TimeSpan _interval;

        public CacheEvictionService(
            IServiceProvider serviceProvider, // Use IServiceProvider to create scopes
            IOptions<CacheSettings> cacheSettings,
            ILogger<CacheEvictionService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cacheSettings = cacheSettings.Value;
            // Set interval based on config, e.g., half the default expiration time, minimum 5 mins
            _interval = TimeSpan.FromMinutes(Math.Max(5, _cacheSettings.DefaultExpirationMinutes / 2.0));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Cache Eviction Service starting. Eviction interval: {Interval}", _interval);
            // Start the timer after a short delay, then run periodically
            _timer = new Timer(DoWork, null, TimeSpan.FromSeconds(15), _interval);
            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogDebug("Cache Eviction Service is running.");

            try
            {
                // Create a scope to resolve scoped services if needed, although ICacheService is likely Singleton here
                using (var scope = _serviceProvider.CreateScope())
                {
                    var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
                    // Use a separate CancellationToken for the background task if needed, or link to application shutdown
                    await cacheService.RemoveExpiredAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during cache eviction.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Cache Eviction Service stopping.");
            _timer?.Change(Timeout.Infinite, 0); // Stop the timer
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
    ```
*   **Outcome:** `CacheEvictionService.cs` implemented.

### 5.2 Configure Dependency Injection (`Host` Project - `Program.cs`)
*   **Action:** Update `Program.cs` to register all the implemented services (interfaces to concrete types) and the hosted service for cache eviction.
*   **File Content (`Program.cs` - DI Section):**
    ```csharp
    // (Includes from Task 02 remain)
    using NuGetContextMcpServer.Application.Interfaces;
    using NuGetContextMcpServer.Application.Services;
    using NuGetContextMcpServer.Infrastructure.Caching;
    using NuGetContextMcpServer.Infrastructure.NuGet;
    // using NuGetContextMcpServer.Infrastructure.Parsing; // Already included

    // --- MSBuild Locator Registration ---
    // ... (as before) ...

    var builder = Host.CreateApplicationBuilder(args);

    // --- Configure Logging ---
    // ... (as before) ...

    // --- Configure Options Pattern ---
    // ... (as before) ...

    // --- Configure Application Services (DI) ---
    // Infrastructure Services (typically Singleton unless they have specific state)
    builder.Services.AddSingleton<ISolutionParser, MsBuildSolutionParser>();
    builder.Services.AddSingleton<IProjectParser, MsBuildProjectParser>();
    builder.Services.AddSingleton<ICacheService, SqliteCacheService>(); // Register SQLite implementation
    builder.Services.AddSingleton<INuGetQueryService, NuGetClientWrapper>();

    // Application Services (can be Singleton if stateless, Scoped/Transient if stateful per call - Singleton likely ok here)
    builder.Services.AddSingleton<IProjectAnalysisService, ProjectAnalysisService>();
    builder.Services.AddSingleton<IPackageSearchService, PackageSearchService>();
    builder.Services.AddSingleton<IPackageVersionService, PackageVersionService>();

    // Background Services
    builder.Services.AddHostedService<CacheEvictionService>();

    // --- Configure MCP Server ---
    // ... (next step) ...

    // --- Build and Run Host ---
    // ... (as before) ...
    ```
*   **Outcome:** All services are registered in the DI container with appropriate lifetimes (mostly Singleton for this application type).

### 5.3 Configure MCP Server (`Host` Project - `Program.cs`)
*   **Action:** Complete the MCP server configuration in `Program.cs` using the `ModelContextProtocol` SDK extensions.
*   **File Content (`Program.cs` - MCP Section):**
    ```csharp
    // (Includes from Task 02 remain)
    // (DI registrations from 5.2 remain)
    using ModelContextProtocol.Extensions.Hosting; // Required for MCP extensions
    using NuGetContextMcpServer.Infrastructure.Mcp; // For NuGetTools class

    // --- MSBuild Locator Registration ---
    // ... (as before) ...

    var builder = Host.CreateApplicationBuilder(args);

    // --- Configure Logging ---
    // ... (as before) ...

    // --- Configure Options Pattern ---
    // ... (as before) ...

    // --- Configure Application Services (DI) ---
    // ... (as before) ...

    // --- Configure MCP Server ---
    builder.Services.AddMcpServer()
       .WithStdioServerTransport() // Use stdio for communication
       .WithToolsFromAssembly(typeof(NuGetTools).Assembly); // Register tools from the assembly containing NuGetTools

    // --- Build and Run Host ---
    // ... (as before) ...
    ```
*   **Outcome:** MCP server services are registered, configured for stdio transport, and set up to discover tools from the `Infrastructure` assembly.

### 5.4 Final Review and Build (`Host` Project - `Program.cs`)
*   **Action:** Review the complete `Program.cs` file to ensure all components are correctly configured and integrated. Attempt to build the solution.
*   **File Content (`Program.cs` - Complete):**
    ```csharp
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using ModelContextProtocol.Extensions.Hosting; // Required for MCP extensions
    using NuGetContextMcpServer.Application.Interfaces;
    using NuGetContextMcpServer.Application.Services;
    using NuGetContextMcpServer.Infrastructure.Caching;
    using NuGetContextMcpServer.Infrastructure.Configuration; // For Settings classes
    using NuGetContextMcpServer.Infrastructure.Mcp; // For NuGetTools class
    using NuGetContextMcpServer.Infrastructure.NuGet;
    using NuGetContextMcpServer.Infrastructure.Parsing; // For MsBuildInitializer

    try // Add top-level try-catch for startup errors
    {
        // --- MSBuild Locator Registration ---
        // Create a temporary logger factory for the initializer if needed
        using var initialLoggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        MsBuildInitializer.EnsureMsBuildRegistered(initialLoggerFactory);
        // --- End MSBuild Locator Registration ---

        var builder = Host.CreateApplicationBuilder(args);

        // --- Configure Logging (Reads from appsettings.json by default) ---
        // Default configuration is usually sufficient, reads from appsettings.json
        // builder.Logging.ClearProviders();
        // builder.Logging.AddConsole();

        // --- Configure Options Pattern ---
        builder.Services.Configure<NuGetSettings>(builder.Configuration.GetSection("NuGetSettings"));
        builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection("CacheSettings"));

        // --- Configure Application Services (DI) ---
        // Infrastructure Services (typically Singleton unless they have specific state)
        builder.Services.AddSingleton<ISolutionParser, MsBuildSolutionParser>();
        builder.Services.AddSingleton<IProjectParser, MsBuildProjectParser>();
        builder.Services.AddSingleton<ICacheService, SqliteCacheService>(); // Register SQLite implementation
        builder.Services.AddSingleton<INuGetQueryService, NuGetClientWrapper>();

        // Application Services (can be Singleton if stateless, Scoped/Transient if stateful per call - Singleton likely ok here)
        builder.Services.AddSingleton<IProjectAnalysisService, ProjectAnalysisService>();
        builder.Services.AddSingleton<IPackageSearchService, PackageSearchService>();
        builder.Services.AddSingleton<IPackageVersionService, PackageVersionService>();

        // Background Services
        builder.Services.AddHostedService<CacheEvictionService>();

        // --- Configure MCP Server ---
        builder.Services.AddMcpServer()
           .WithStdioServerTransport() // Use stdio for communication
           .WithToolsFromAssembly(typeof(NuGetTools).Assembly); // Register tools from the assembly containing NuGetTools

        // --- Build and Run Host ---
        var host = builder.Build();

        var mainLogger = host.Services.GetRequiredService<ILogger<Program>>();
        mainLogger.LogInformation("Host built. Starting NuGet Context MCP Server...");

        await host.RunAsync(); // Runs the MCP server loop

        mainLogger.LogInformation("NuGet Context MCP Server shutting down.");

    }
    catch (Exception ex)
    {
        // Log any critical startup errors
        Console.Error.WriteLine($"FATAL ERROR: Application failed to start. {ex}");
        // Ensure logs are flushed if using non-console providers
        await Task.Delay(1000); // Give time for logs to flush potentially
        Environment.Exit(1); // Exit with non-zero code
    }
    ```
*   **Outcome:** The solution should build successfully. `Program.cs` is complete, integrating all components. The application is ready for testing and execution as an MCP server.

---