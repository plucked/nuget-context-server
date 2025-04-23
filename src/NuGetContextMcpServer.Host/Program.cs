using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog; 
using NuGetContextMcpServer.Abstractions.Interfaces;
using NuGetContextMcpServer.Application.Services;
using NuGetContextMcpServer.Infrastructure.Caching;
using NuGetContextMcpServer.Infrastructure.Configuration; 
using NuGetContextMcpServer.Application.Mcp; 
using NuGetContextMcpServer.Infrastructure.NuGet;
using NuGetContextMcpServer.Infrastructure.Parsing; 

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting NuGet Context MCP Server host setup...");

try
{
    using var serilogLoggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger);
    MsBuildInitializer.EnsureMsBuildRegistered(serilogLoggerFactory);

    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration) 
        .Enrich.FromLogContext()
        .WriteTo.File(
            "log-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
            retainedFileCountLimit: 7,
            buffered: false)
        .CreateLogger());

    builder.Services.Configure<NuGetSettings>(builder.Configuration.GetSection("NuGetSettings"));
    builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection("CacheSettings"));

    builder.Services.AddSingleton<ISolutionParser, MsBuildSolutionParser>();
    builder.Services.AddSingleton<IProjectParser, MsBuildProjectParser>();
    builder.Services.AddSingleton<ICacheService, SqliteCacheService>(); 
    builder.Services.AddSingleton<INuGetQueryService, NuGetClientWrapper>();

    builder.Services.AddSingleton<IProjectAnalysisService, ProjectAnalysisService>();
    builder.Services.AddSingleton<IPackageSearchService, PackageSearchService>();
    builder.Services.AddSingleton<IPackageVersionService, PackageVersionService>();
    builder.Services.AddSingleton<IPackageMetadataService, PackageMetadataService>();

    builder.Services.AddHostedService<CacheEvictionService>();

    builder.Services.AddMcpServer()
       .WithStdioServerTransport() 
       .WithToolsFromAssembly(typeof(NuGetTools).Assembly); 

    var host = builder.Build();

    var mainLogger = host.Services.GetRequiredService<ILogger<Program>>();
    mainLogger.LogInformation("Host built. Starting NuGet Context MCP Server...");

    await host.RunAsync(); 

    mainLogger.LogInformation("NuGet Context MCP Server shutting down");

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "FATAL ERROR: Application failed to start");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
