using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Application.Services;
using NuGetContextMcpServer.Infrastructure.Caching;
using NuGetContextMcpServer.Infrastructure.Configuration; // For Settings classes
using NuGetContextMcpServer.Application.Mcp; // For NuGetTools class - Corrected namespace
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
