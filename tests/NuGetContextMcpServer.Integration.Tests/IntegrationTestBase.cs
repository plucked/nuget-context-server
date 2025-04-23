using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuGetContextMcpServer.Infrastructure.Parsing;
using NUnit.Framework;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Reflection; // Added for Assembly
using NuGetContextMcpServer.Abstractions.Interfaces; // Added for parser interfaces
// Removed duplicate: using NuGetContextMcpServer.Infrastructure.Parsing;
using Microsoft.Extensions.Logging; // Added for logging configuration
using System.Threading.Tasks; // Added for async Task
using NuGet.Protocol; // Added for NuGet.Protocol interaction
using NuGet.Protocol.Core.Types; // Added for NuGet.Protocol interaction
using NuGet.Configuration; // Added for NuGet.Protocol interaction
using NuGet.Common; // Added for NuGet.Protocol interaction (NullLogger)
using System.Threading; // Added for CancellationToken
using System; // Added for ArgumentNullException, Exception, IDisposable

namespace NuGetContextMcpServer.Integration.Tests;

[TestFixture]
public abstract class IntegrationTestBase
{
    private IHost? _host;
    protected IServiceProvider ServiceProvider { get; private set; } = null!; // Initialized in OneTimeSetUp

    // Constants and fields for test package handling
    protected const string TestApiKey = "TEST_KEY"; // API Key for test feed
    private static readonly string _repositoryRoot = FindRepositoryRoot();
    private static readonly string[] _testPackageProjectPaths =
    [
        Path.Combine(_repositoryRoot, "tests", "TestPackages", "TestPackageA", "TestPackageA.csproj"),
        Path.Combine(_repositoryRoot, "tests", "TestPackages", "TestPackageB", "TestPackageB.csproj")
    ];
    private string? _tempPackDirectory; // Stores path to packed .nupkg files

    [OneTimeSetUp]
    public virtual async Task OneTimeSetUp() // Reverted to async Task for override compatibility
    {
        // CRITICAL: Ensure MSBuild is located and registered before any MSBuild operations
        // This is synchronous, so no await needed here.
        MsBuildInitializer.EnsureMsBuildRegistered();

        var builder = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile("appsettings.Testing.json", optional: false, reloadOnChange: true);
                // Allow derived classes to add more config sources later
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders(); // Optional: Remove other loggers if desired
                logging.AddConsole();
                logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug); // Fully qualified LogLevel
            })
            .ConfigureServices((context, services) =>
            {
                // Register base services needed for all integration tests
                services.AddSingleton<ISolutionParser, MsBuildSolutionParser>();
                services.AddSingleton<IProjectParser, MsBuildProjectParser>();
                // Derived classes will register specific services via ConfigureTestHost
            });

        // Allow derived classes to customize configuration and services before building
        ConfigureTestHost(builder);

        _host = builder.Build();
        ServiceProvider = _host.Services;
 
        // NOTE: Package setup is now the responsibility of derived classes
        // that require a feed, as the feed URL might be dynamic (e.g., Testcontainers).
 
        await Task.CompletedTask; // Add await to satisfy CS1998
    }

    /// <summary>
    /// Allows derived test fixtures to configure the IHostBuilder before it's built.
    /// Use this to add specific configuration sources (like InMemoryCollection for overrides)
    /// or register services required by the specific test fixture.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    protected virtual void ConfigureTestHost(IHostBuilder builder)
    {
        // Base implementation does nothing, derived classes override this.
    }


    [OneTimeTearDown]
    public virtual async Task OneTimeTearDown() // Reverted to async Task for override compatibility
    {
        // NOTE: Package cleanup (temp directory) is handled here,
        // but feed-specific cleanup (like container disposal) is up to derived classes.
        // This is synchronous, so no await needed here.
        CleanupTestNuGetPackages();

        // Dispose the host
        (_host as IDisposable)?.Dispose();
        _host = null;

        // NOTE: Cache file deletion is now the responsibility of derived classes
        // that configure a specific cache path.
 
        await Task.CompletedTask; // Add await to satisfy CS1998
    }

    protected T GetRequiredService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Packs the test projects and pushes them to the specified NuGet feed using NuGet.Protocol.
    /// </summary>
    /// <param name="feedUrl">The URL of the NuGet feed to push packages to (must include /v3/index.json for NuGet.Protocol).</param>
    /// <param name="apiKey">The API key required for the push operation.</param>
    protected async Task SetupTestNuGetPackages(string feedUrl, string apiKey) // Made async Task
    {
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            throw new ArgumentNullException(nameof(feedUrl), "Feed URL cannot be null or empty.");
        }
        if (string.IsNullOrWhiteSpace(apiKey))
        {
             throw new ArgumentNullException(nameof(apiKey), "API key cannot be null or empty.");
        }

        // Ensure feedUrl ends with /v3/index.json for NuGet.Protocol V3 interaction
        string serviceIndexUrl = feedUrl.EndsWith("/v3/index.json") ? feedUrl : feedUrl.TrimEnd('/') + "/v3/index.json";

        _tempPackDirectory = Path.Combine(Path.GetTempPath(), "nuget-test-packages-" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPackDirectory);
        Console.WriteLine($"Created temporary pack directory: {_tempPackDirectory}");

        // 1. Pack the projects (using existing ExecuteProcess)
        foreach (var projectPath in _testPackageProjectPaths)
        {
            Console.WriteLine($"Packing project: {projectPath}");
            var packArgs = $"pack \"{projectPath}\" -o \"{_tempPackDirectory}\" --configuration Release";
            // ExecuteProcess is synchronous, fine for this part
            ExecuteProcess("dotnet", packArgs, $"Failed to pack project {projectPath}.");
        }

        // 2. Push the packages using NuGet.Protocol
        var nupkgFiles = Directory.GetFiles(_tempPackDirectory, "*.nupkg");
        if (nupkgFiles.Length == 0)
        {
             Assert.Fail($"No .nupkg files found in the temporary directory '{_tempPackDirectory}' after packing.");
        }

        // Setup NuGet.Protocol resources
        var packageSource = new PackageSource(serviceIndexUrl);
        // Note: Credentials are not needed here as API key is passed directly to Push method
        var repository = Repository.Factory.GetCoreV3(packageSource);
        var pushResource = await repository.GetResourceAsync<PackageUpdateResource>(CancellationToken.None);
        if (pushResource == null)
        {
            Assert.Fail($"Could not get PackageUpdateResource from feed: {serviceIndexUrl}");
            return; // Keep compiler happy
        }
        var nugetLogger = NullLogger.Instance; // Use NullLogger or a configured logger

        foreach (var nupkgPath in nupkgFiles)
        {
            Console.WriteLine($"Pushing package using NuGet.Protocol: {nupkgPath} to {serviceIndexUrl}");
            try
            {
#pragma warning disable CS0618 // Obsolete overload is used intentionally here. Refactoring to multi-path push is a larger change.
                await pushResource.Push(
                    packagePath: nupkgPath,
                    symbolSource: null, // No symbols
                    timeoutInSecond: 120, // Added back with correct name
                    disableBuffering: false,
                    getApiKey: source => apiKey, // Provide the API key
                    getSymbolApiKey: null,
                    noServiceEndpoint: false, // Let NuGet.Protocol handle endpoint resolution from index
                    skipDuplicate: true, // Skip if already exists (useful for re-runs)
                    symbolPackageUpdateResource: null, // Added missing parameter
                    log: nugetLogger);
#pragma warning restore CS0618
            }
            catch (Exception ex)
            {
                 // Wrap exception in Assert.Fail for clearer test output
                 Assert.Fail($"NuGet.Protocol push failed for {nupkgPath}: {ex}");
            }
        }

        Console.WriteLine("Finished setting up test NuGet packages using NuGet.Protocol.");
    }

    /// <summary>
    /// Cleans up the temporary directory created for packing test NuGet packages.
    /// </summary>
    protected void CleanupTestNuGetPackages() // Remains synchronous
    {
        if (!string.IsNullOrEmpty(_tempPackDirectory) && Directory.Exists(_tempPackDirectory))
        {
            try
            {
                Console.WriteLine($"Cleaning up temporary pack directory: {_tempPackDirectory}");
                Directory.Delete(_tempPackDirectory, true);
                _tempPackDirectory = null; // Reset field after successful deletion
            }
            catch (Exception ex)
            {
                // Log or output the error, but don't fail the teardown if cleanup fails
                Console.Error.WriteLine($"Error cleaning up temporary directory '{_tempPackDirectory}': {ex.Message}");
                // Consider adding more robust logging if this becomes an issue
            }
        }
    }

    private static void ExecuteProcess(string fileName, string arguments, string failureMessage)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // Set working directory if needed, though dotnet commands usually handle paths well
            // WorkingDirectory = "..."
        };

        var process = new Process { StartInfo = processStartInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, args) => { if (args.Data != null) outputBuilder.AppendLine(args.Data); };
        process.ErrorDataReceived += (sender, args) => { if (args.Data != null) errorBuilder.AppendLine(args.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit(); // Consider adding a timeout

        if (process.ExitCode != 0)
        {
            var errorMessage = $"{failureMessage}\nExit Code: {process.ExitCode}\nStdOut:\n{outputBuilder}\nStdErr:\n{errorBuilder}";
            Console.Error.WriteLine(errorMessage); // Log to console for visibility
            Assert.Fail(errorMessage);
        }
        else
        {
             // Optionally log success/output even on success for debugging
             Console.WriteLine($"Command succeeded: {fileName} {arguments}");
             // Console.WriteLine($"Output:\n{outputBuilder}");
        }
    }

    private static string FindRepositoryRoot()
    {
        var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var currentDirectory = assemblyLocation;

        while (currentDirectory != null)
        {
            if (File.Exists(Path.Combine(currentDirectory, "NuGetContextMcpServer.sln")))
            {
                return currentDirectory;
            }
            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }

        throw new DirectoryNotFoundException($"Could not find repository root containing 'NuGetContextMcpServer.sln' starting from {assemblyLocation}.");
    }
}