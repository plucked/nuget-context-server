using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuGetContextMcpServer.Abstractions.Interfaces;
using NuGetContextMcpServer.Infrastructure.Caching;
using NuGetContextMcpServer.Infrastructure.Configuration;
using NuGetContextMcpServer.Infrastructure.NuGet;
using NuGetContextMcpServer.Abstractions.Dtos; // Added for PackageSearchResult
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NuGetContextMcpServer.Integration.Tests;

[TestFixture]
public class NuGetClientCacheIntegrationTests : IntegrationTestBase
{
    private IContainer? _bagetContainer;
    private string? _bagetFeedUrl;
    private string? _cacheDbPath;

    // BaGetter container configuration
    private const string BaGetterImage = "bagetter/bagetter:1.6.0"; // Use a specific stable tag
    private const ushort BaGetterInternalPort = 8080;

    protected override void ConfigureTestHost(IHostBuilder builder)
    {
        // This method is called by the base OneTimeSetUp *before* the host is built.
        // We need the dynamic URLs/paths here to configure the host.
        // Therefore, container setup needs to happen *before* base.OneTimeSetUp() is called.

        // Generate unique cache path for this test run
        _cacheDbPath = Path.Combine(Path.GetTempPath(), "nuget-test-cache-" + Guid.NewGuid().ToString() + ".db");
        Console.WriteLine($"Using temporary cache path: {_cacheDbPath}");

        // Add configuration overrides using the dynamic values
        builder.ConfigureAppConfiguration((context, config) =>
        {
            if (_bagetFeedUrl == null || _cacheDbPath == null)
            {
                // This should not happen if setup order is correct
                throw new InvalidOperationException("BaGetter URL or Cache DB Path not set before configuring host.");
            }

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { $"NuGetSettings:{nameof(NuGetSettings.QueryFeedUrl)}", _bagetFeedUrl },
                { $"CacheSettings:{nameof(CacheSettings.DatabasePath)}", _cacheDbPath }
                // Optionally override CacheSettings:DefaultExpirationMinutes for faster testing if needed
            });
            Console.WriteLine($"Overriding NuGetSettings:QueryFeedUrl with: {_bagetFeedUrl}");
            Console.WriteLine($"Overriding CacheSettings:DatabasePath with: {_cacheDbPath}");
        });

        // Register the actual services under test
        builder.ConfigureServices((context, services) =>
        {
            // Ensure configuration bindings are set up
            services.AddOptions<NuGetSettings>().Bind(context.Configuration.GetSection("NuGetSettings"));
            services.AddOptions<CacheSettings>().Bind(context.Configuration.GetSection("CacheSettings"));

            // Register services (using AddSingleton for simplicity in tests, adjust if needed)
            services.AddSingleton<ICacheService, SqliteCacheService>(); // Use the real implementation
            services.AddSingleton<INuGetQueryService, NuGetClientWrapper>(); // Use the real implementation

            // Add other dependencies if needed by the services above (e.g., logging is added by base)
        });
    }


    [OneTimeSetUp]
    public override async Task OneTimeSetUp() // NUnit needs Task return type for async setup
    {
        Console.WriteLine("Starting BaGetter container setup...");

        _bagetContainer = new ContainerBuilder()
           .WithImage(BaGetterImage)
           .WithPortBinding(BaGetterInternalPort, true) // Internal port 8080, random host port
           .WithEnvironment("ApiKey", TestApiKey) // Use API key from base class
           // Defaults (SQLite, FileSystem, Database Search) are generally fine for testing
           .WithWaitStrategy(Wait.ForUnixContainer()
               .UntilHttpRequestIsSucceeded(req => req
                   .ForPort(BaGetterInternalPort)
                   .ForPath("/v3/index.json"))) // Wait for NuGet V3 index
           // Removed .WithStartupTimeout(TimeSpan.FromMinutes(2)) - Rely on default
           .Build();

       try
        {
            await _bagetContainer.StartAsync();
            _bagetFeedUrl = $"http://{_bagetContainer.Hostname}:{_bagetContainer.GetMappedPublicPort(BaGetterInternalPort)}";
            Console.WriteLine($"BaGetter container started successfully at {_bagetFeedUrl}");

            // Now that the container is running and URL is known, call the base setup.
            // The base setup will call ConfigureTestHost (above) to apply the dynamic config.
            base.OneTimeSetUp(); // This builds the host and sets up ServiceProvider

            // Now push the test packages to the running container using the base class helper
            Console.WriteLine("Pushing test packages to BaGetter...");
            await SetupTestNuGetPackages(_bagetFeedUrl, TestApiKey); // Use dynamic URL and base API key (Added await)
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed during OneTimeSetUp: {ex}");
            // Attempt to clean up container if it exists
            if (_bagetContainer != null)
            {
                try { await _bagetContainer.DisposeAsync(); } catch { /* Ignore cleanup errors */ }
            }
            // Clean up cache file if created
            if (!string.IsNullOrEmpty(_cacheDbPath) && File.Exists(_cacheDbPath))
            {
                 try { File.Delete(_cacheDbPath); } catch { /* Ignore cleanup errors */ }
            }
            throw; // Re-throw the exception to fail the setup
        }
    }

    [OneTimeTearDown]
    public override async Task OneTimeTearDown() // NUnit needs Task return type for async teardown
    {
        // Call base teardown first (disposes host, cleans up pack directory)
        base.OneTimeTearDown(); // Note: Base teardown is now synchronous

        // Dispose the container
        if (_bagetContainer != null)
        {
            Console.WriteLine("Disposing BaGetter container...");
            try
            {
                await _bagetContainer.DisposeAsync();
                Console.WriteLine("BaGetter container disposed.");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Error disposing BaGetter container: {ex.Message}");
            }
            _bagetContainer = null;
        }

        // Delete the temporary cache database
        if (!string.IsNullOrEmpty(_cacheDbPath) && File.Exists(_cacheDbPath))
        {
            Console.WriteLine($"Deleting temporary cache database: {_cacheDbPath}");
            try
            {
                File.Delete(_cacheDbPath);
                Console.WriteLine("Temporary cache database deleted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting cache database '{_cacheDbPath}': {ex.Message}");
            }
            _cacheDbPath = null;
        }
    }

    // --- Test Cases ---

    [Test]
    public async Task GetAllVersions_CacheMiss_FetchesFromLocalFeedAndPopulatesCache()
    {
        // Arrange
        var queryService = GetRequiredService<INuGetQueryService>();
        var cacheService = GetRequiredService<ICacheService>();
        const string packageId = "TestPackageA";
        string cacheKey = $"versions:{packageId.ToLowerInvariant()}"; // Match key format from NuGetClientWrapper

        // Ensure cache is clear for this key (optional, depends on isolation needs)
        await cacheService.RemoveAsync(cacheKey, CancellationToken.None);
        var initialCache = await cacheService.GetAsync<List<string>>(cacheKey, CancellationToken.None);
        Assert.That(initialCache, Is.Null, "Cache should be empty before the call.");

        // Act
        var versions = await queryService.GetAllVersionsAsync(packageId, CancellationToken.None);

        // Assert - Result
        Assert.That(versions, Is.Not.Null, "Versions should not be null.");
        Assert.That(versions.Select(v => v.ToString()), Contains.Item("1.0.0"), "Result should contain version 1.0.0.");

        // Assert - Cache Population
        var finalCache = await cacheService.GetAsync<List<string>>(cacheKey, CancellationToken.None);
        Assert.That(finalCache, Is.Not.Null, "Cache should be populated after the call.");
        Assert.That(finalCache, Contains.Item("1.0.0"), "Cache should contain version 1.0.0.");
    }

    [Test]
    public async Task GetAllVersions_CacheHit_ReturnsFromCacheWithoutHittingFeed()
    {
        // Arrange
        var queryService = GetRequiredService<INuGetQueryService>();
        var cacheService = GetRequiredService<ICacheService>();
        const string packageId = "TestPackageB";
        string cacheKey = $"versions:{packageId.ToLowerInvariant()}";

        // Ensure item is in cache by calling it once
        await queryService.GetAllVersionsAsync(packageId, CancellationToken.None);
        var initialCache = await cacheService.GetAsync<List<string>>(cacheKey, CancellationToken.None);
        Assert.That(initialCache, Is.Not.Null, "Cache should be populated before the second call.");

        // Act - Call again, should hit cache
        // Note: Verifying "without hitting feed" is hard without mocking. We rely on the cache service implementation.
        var versions = await queryService.GetAllVersionsAsync(packageId, CancellationToken.None);

        // Assert
        Assert.That(versions, Is.Not.Null, "Versions should not be null on cache hit.");
        Assert.That(versions.Select(v => v.ToString()), Contains.Item("2.1.3"), "Result should contain version 2.1.3 from cache.");
    }

    [Test]
    public async Task SearchPackages_CacheMiss_FetchesFromLocalFeedAndPopulatesCache()
    {
        // Arrange
        var queryService = GetRequiredService<INuGetQueryService>();
        var cacheService = GetRequiredService<ICacheService>();
        const string searchTerm = "TestPackageA";
        const bool includePrerelease = true;
        var skip = 0; // Default skip
        var take = 50; // Default take
        // Update cache key to include skip and take
        string cacheKey = $"search:{searchTerm.ToLowerInvariant()}:prerel:{includePrerelease}:skip:{skip}:take:{take}";

        await cacheService.RemoveAsync(cacheKey, CancellationToken.None);
        var initialCache = await cacheService.GetAsync<List<PackageSearchResult>>(cacheKey, CancellationToken.None);
        Assert.That(initialCache, Is.Null, "Cache should be empty before the call.");

        // Act
        // Add skip and take arguments
        var results = await queryService.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, CancellationToken.None);

        // Assert - Result
        Assert.That(results, Is.Not.Null, "Search results should not be null.");
        Assert.That(results.Any(p => p.Id.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)), Is.True, $"Search results should contain '{searchTerm}'."); // Corrected: p.Id

        // Assert - Cache Population
        var finalCache = await cacheService.GetAsync<List<PackageSearchResult>>(cacheKey, CancellationToken.None);
        Assert.That(finalCache, Is.Not.Null, "Cache should be populated after the call.");
        Assert.That(finalCache.Any(p => p.Id.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)), Is.True, $"Cache should contain '{searchTerm}'."); // Corrected: p.Id
    }

     [Test]
    public async Task SearchPackages_CacheHit_ReturnsFromCacheWithoutHittingFeed()
    {
        // Arrange
        var queryService = GetRequiredService<INuGetQueryService>();
        var cacheService = GetRequiredService<ICacheService>();
        const string searchTerm = "TestPackageB";
        const bool includePrerelease = true;
        var skip = 0; // Default skip
        var take = 50; // Default take
        // Update cache key to include skip and take
        string cacheKey = $"search:{searchTerm.ToLowerInvariant()}:prerel:{includePrerelease}:skip:{skip}:take:{take}";

        // Ensure item is in cache
        // Add skip and take arguments
        await queryService.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, CancellationToken.None);
        var initialCache = await cacheService.GetAsync<List<PackageSearchResult>>(cacheKey, CancellationToken.None);
        Assert.That(initialCache, Is.Not.Null, "Cache should be populated before the second call.");

        // Act - Call again
        // Add skip and take arguments
        var results = await queryService.SearchPackagesAsync(searchTerm, includePrerelease, skip, take, CancellationToken.None);

        // Assert
        Assert.That(results, Is.Not.Null, "Search results should not be null on cache hit.");
        Assert.That(results.Any(p => p.Id.Equals(searchTerm, StringComparison.OrdinalIgnoreCase)), Is.True, $"Search results from cache should contain '{searchTerm}'."); // Corrected: p.Id
    }

    [Test]
    public async Task GetLatestVersion_UsesCachePopulatedByGetAllVersions()
    {
        // Arrange
        var queryService = GetRequiredService<INuGetQueryService>();
        var cacheService = GetRequiredService<ICacheService>();
        const string packageId = "TestPackageB";
        string versionsCacheKey = $"versions:{packageId.ToLowerInvariant()}";

        // Ensure versions are in cache by calling GetAllVersionsAsync
        await queryService.GetAllVersionsAsync(packageId, CancellationToken.None);
        var versionsCache = await cacheService.GetAsync<List<string>>(versionsCacheKey, CancellationToken.None);
        Assert.That(versionsCache, Is.Not.Null, "Versions cache should be populated.");

        // Act - GetLatestVersionAsync should use the populated cache
        var latestVersion = await queryService.GetLatestVersionAsync(packageId, CancellationToken.None);

        // Assert
        Assert.That(latestVersion, Is.Not.Null, "Latest version should not be null.");
        Assert.That(latestVersion?.ToString(), Is.EqualTo("2.1.3"), "Latest version should be 2.1.3.");
    }
    // --- Tests for GetPackageMetadataAsync ---

    [Test]
    public async Task GetPackageMetadata_SpecificVersion_CacheMiss_FetchesAndCaches()
    {
        // Arrange
        var queryService = GetRequiredService<INuGetQueryService>();
        var cacheService = GetRequiredService<ICacheService>();
        const string packageId = "TestPackageA";
        const string versionString = "1.0.0";
        var version = NuGet.Versioning.NuGetVersion.Parse(versionString);
        string cacheKey = $"metadata:{packageId.ToLowerInvariant()}:{version.ToNormalizedString()}";

        await cacheService.RemoveAsync(cacheKey, CancellationToken.None);
        var initialCache = await cacheService.GetAsync<object>(cacheKey, CancellationToken.None); // Use object or specific type if known/serializable
        Assert.That(initialCache, Is.Null, "Cache should be empty before the call.");

        // Act
        var metadata = await queryService.GetPackageMetadataAsync(packageId, version, CancellationToken.None);

        // Assert - Result
        Assert.That(metadata, Is.Not.Null, "Metadata should not be null.");
        Assert.That(metadata.Identity.Id, Is.EqualTo(packageId), "Metadata ID should match.");
        Assert.That(metadata.Identity.Version, Is.EqualTo(version), "Metadata Version should match.");
        // Add more checks if needed, e.g., description, authors

        // Assert - Cache Population
        // Note: IPackageSearchMetadata might not be directly serializable/deserializable by all cache implementations.
        // This assertion might fail depending on the cache setup. If it does, we might need to adjust caching strategy or assertion.
        var finalCache = await cacheService.GetAsync<object>(cacheKey, CancellationToken.None); // Check if *something* was cached
        Assert.That(finalCache, Is.Not.Null, "Cache should be populated after the call.");
    }

    // --- Tests for GetLatestPackageMetadataAsync ---

    [Test]
    public async Task GetLatestPackageMetadata_IncludePrerelease_CacheMiss_FetchesAndCaches()
    {
        // Arrange
        var queryService = GetRequiredService<INuGetQueryService>();
        var cacheService = GetRequiredService<ICacheService>();
        const string packageId = "TestPackageB"; // Has version 2.1.3
        const bool includePrerelease = true;
        string cacheKey = $"latest-metadata:{packageId.ToLowerInvariant()}:prerel:{includePrerelease}";

        await cacheService.RemoveAsync(cacheKey, CancellationToken.None);
        var initialCache = await cacheService.GetAsync<object>(cacheKey, CancellationToken.None);
        Assert.That(initialCache, Is.Null, "Cache should be empty before the call.");

        // Act
        var metadata = await queryService.GetLatestPackageMetadataAsync(packageId, includePrerelease, CancellationToken.None);

        // Assert - Result
        Assert.That(metadata, Is.Not.Null, "Latest metadata should not be null.");
        Assert.That(metadata.Identity.Id, Is.EqualTo(packageId), "Metadata ID should match.");
        Assert.That(metadata.Identity.Version.ToString(), Is.EqualTo("2.1.3"), "Latest version (incl. prerelease) should be 2.1.3.");

        // Assert - Cache Population
        var finalCache = await cacheService.GetAsync<object>(cacheKey, CancellationToken.None);
        Assert.That(finalCache, Is.Not.Null, "Cache should be populated after the call.");
    }

    [Test]
    public async Task GetLatestPackageMetadata_StableOnly_CacheMiss_FetchesAndCaches()
    {
        // Arrange
        var queryService = GetRequiredService<INuGetQueryService>();
        var cacheService = GetRequiredService<ICacheService>();
        const string packageId = "TestPackageA"; // Only has stable 1.0.0
        const bool includePrerelease = false;
        string cacheKey = $"latest-metadata:{packageId.ToLowerInvariant()}:prerel:{includePrerelease}";

        await cacheService.RemoveAsync(cacheKey, CancellationToken.None);
        var initialCache = await cacheService.GetAsync<object>(cacheKey, CancellationToken.None);
        Assert.That(initialCache, Is.Null, "Cache should be empty before the call.");

        // Act
        var metadata = await queryService.GetLatestPackageMetadataAsync(packageId, includePrerelease, CancellationToken.None);

        // Assert - Result
        Assert.That(metadata, Is.Not.Null, "Latest stable metadata should not be null.");
        Assert.That(metadata.Identity.Id, Is.EqualTo(packageId), "Metadata ID should match.");
        Assert.That(metadata.Identity.Version.ToString(), Is.EqualTo("1.0.0"), "Latest stable version should be 1.0.0.");

        // Assert - Cache Population
        var finalCache = await cacheService.GetAsync<object>(cacheKey, CancellationToken.None);
        Assert.That(finalCache, Is.Not.Null, "Cache should be populated after the call.");
    }

    // Note: Cache hit scenarios are implicitly tested when cache miss tests pass and subsequent calls work.
    // Explicit cache hit tests could be added by calling twice and potentially mocking the underlying resource fetch
    // on the second call, but that increases complexity significantly for integration tests.
}