using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog; // Add Serilog using
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Application.Services;
using NuGetContextMcpServer.Infrastructure.Caching;
using NuGetContextMcpServer.Infrastructure.Configuration; // For Settings classes
using NuGetContextMcpServer.Application.Mcp; // For NuGetTools class - Corrected namespace
using NuGetContextMcpServer.Infrastructure.NuGet;
using NuGetContextMcpServer.Infrastructure.Parsing; // For MsBuildInitializer

// Configure Serilog logger first (outside the main try block for startup logging)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console() // Basic console logging during startup
    .CreateBootstrapLogger(); // Use bootstrap logger until host is built

Log.Information("Starting NuGet Context MCP Server host setup...");

try // Add top-level try-catch for startup errors
{
    // --- MSBuild Locator Registration ---
    // Use Serilog's logger for MSBuild initialization
    // Create a Microsoft ILogger wrapper around the Serilog bootstrap logger
    using var serilogLoggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger);
    MsBuildInitializer.EnsureMsBuildRegistered(serilogLoggerFactory); // Pass the ILoggerFactory instance
    // --- End MSBuild Locator Registration ---

    var builder = Host.CreateApplicationBuilder(args);

    // --- Configure Serilog ---
    // Clear default providers and add Serilog via Logging builder
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration) // Read base config from appsettings.json
        .Enrich.FromLogContext()
        // Explicitly add file sink here as well to ensure it's configured
        .WriteTo.File(
            "log-.log", // Use .log extension
            rollingInterval: RollingInterval.Day, // Use the same rolling interval
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}", // Use the same template
            retainedFileCountLimit: 7, // Use the same retention
            buffered: false) // Use the same buffering setting
        .CreateLogger()); // Create logger instance

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

    return 0; // Indicate successful shutdown
}
catch (Exception ex)
{
    // Log any critical startup errors using Serilog
    Log.Fatal(ex, "FATAL ERROR: Application failed to start.");
    return 1; // Indicate failure
}
finally
{
    // Ensure logs are flushed regardless of success or failure
    await Log.CloseAndFlushAsync();
}
