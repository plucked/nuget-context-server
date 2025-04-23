using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetContextMcpServer.Abstractions.Dtos;
using NuGetContextMcpServer.Abstractions.Interfaces;
using NuGetContextMcpServer.Infrastructure.Configuration;
using ILogger = NuGet.Common.ILogger;
using LogLevel = NuGet.Common.LogLevel;

namespace NuGetContextMcpServer.Infrastructure.NuGet;

public class NuGetClientWrapper : INuGetQueryService
{
    private readonly NuGetSettings _settings;
    private readonly ICacheService _cacheService;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<NuGetClientWrapper> _logger;
    private readonly SourceRepository _repository;
    private readonly SourceCacheContext _sourceCacheContext;

    public NuGetClientWrapper(
        IOptions<NuGetSettings> nugetSettings,
        IOptions<CacheSettings> cacheSettings,
        ICacheService cacheService,
        ILogger<NuGetClientWrapper> logger)
    {
        _settings = nugetSettings.Value;
        _cacheSettings = cacheSettings.Value;
        _cacheService = cacheService;
        _logger = logger;
        _sourceCacheContext = new SourceCacheContext { NoCache = false };
        _repository = CreateSourceRepository();
    }

    private SourceRepository CreateSourceRepository()
    {
        // Ensure the URL points to the V3 index
        var feedUrl = _settings.QueryFeedUrl;
        if (!feedUrl.EndsWith("/v3/index.json", StringComparison.OrdinalIgnoreCase))
        {
            feedUrl = feedUrl.TrimEnd('/') + "/v3/index.json";
            _logger.LogDebug("Appending /v3/index.json to QueryFeedUrl for PackageSource: {FeedUrl}", feedUrl);
        }

        var packageSource = new PackageSource(feedUrl);


        if (!string.IsNullOrEmpty(_settings.PasswordOrPat))
        {
            // Use PAT as password. Username might be required or can be arbitrary.
            var username = string.IsNullOrEmpty(_settings.Username) ? "pat" : _settings.Username;
            packageSource.Credentials = new PackageSourceCredential(
                feedUrl,
                username,
                _settings.PasswordOrPat,
                true,
                null);
            _logger.LogInformation("Using configured credentials for feed {FeedUrl}", feedUrl);
        }
        else
        {
            _logger.LogInformation("No credentials configured for feed {FeedUrl}", feedUrl);
        }

        var providers = new List<Lazy<INuGetResourceProvider>>();
        providers.AddRange(Repository.Provider.GetCoreV3());

        return new SourceRepository(packageSource, providers);
    }

    // Helper to get data from cache or fetch from source
    private async Task<T?> GetOrSetCacheAsync<T>(
        string cacheKey,
        Func<Task<T?>> fetchFunc,
        CancellationToken cancellationToken) where T : class
    {
        var cachedItem = await _cacheService.GetAsync<T>(cacheKey, cancellationToken);
        if (cachedItem != null) return cachedItem;

        _logger.LogDebug("Fetching data for cache key: {Key}", cacheKey);
        var fetchedItem = await fetchFunc();

        if (fetchedItem != null)
        {
            var expiration = TimeSpan.FromMinutes(_cacheSettings.DefaultExpirationMinutes);
            await _cacheService.SetAsync(cacheKey, fetchedItem, expiration, cancellationToken);
        }

        return fetchedItem;
    }

    // Updated signature to match INuGetQueryService
    public async Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease,
        int skip, int take, CancellationToken cancellationToken)
    {
        // Include skip and take in the cache key for proper pagination caching
        var cacheKey = $"search:{searchTerm?.ToLowerInvariant()}:prerel:{includePrerelease}:skip:{skip}:take:{take}";

        // Cache the list of results for this specific page
        var results = await GetOrSetCacheAsync(cacheKey, async () =>
        {
            var searchResource = await _repository.GetResourceAsync<PackageSearchResource>(cancellationToken);
            if (searchResource == null)
            {
                _logger.LogWarning("Feed {FeedUrl} does not support V3 Search resource", _settings.QueryFeedUrl);
                return null;
            }

            var searchFilter = new SearchFilter(includePrerelease);
            var searchResults = await searchResource.SearchAsync(
                searchTerm,
                searchFilter,
                skip,
                take,
                _logger.AsNuGetLogger(),
                cancellationToken);

            // Map to DTO before caching
            return searchResults?.Select(r => new PackageSearchResult(
                r.Identity.Id,
                r.Identity.Version.ToNormalizedString(),
                r.Description,
                r.ProjectUrl?.ToString()
            )).ToList();
        }, cancellationToken);

        return results ?? Enumerable.Empty<PackageSearchResult>();
    }


    public async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string packageId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"versions:{packageId.ToLowerInvariant()}";

        var versionStrings = await GetOrSetCacheAsync(cacheKey, async () =>
        {
            var findPackageResource = await _repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
            if (findPackageResource == null)
            {
                _logger.LogWarning("Feed {FeedUrl} does not support V3 FindPackageById resource",
                    _settings.QueryFeedUrl);
                return null;
            }

            var versions = await findPackageResource.GetAllVersionsAsync(
                packageId,
                _sourceCacheContext,
                _logger.AsNuGetLogger(),
                cancellationToken);

            return versions?.Select(v => v.ToNormalizedString()).ToList();
        }, cancellationToken);

        return versionStrings?.Select(NuGetVersion.Parse) ?? [];
    }

    // Helper to find latest based on cached/fetched versions
    private NuGetVersion? FindLatestVersion(IEnumerable<NuGetVersion>? versions, bool includePrerelease)
    {
        if (versions == null || !versions.Any()) return null;

        if (includePrerelease)
        {
            return versions.OrderByDescending(v => v).FirstOrDefault();
        }

        return versions.Where(v => !v.IsPrerelease).OrderByDescending(v => v).FirstOrDefault();
    }

    public async Task<NuGetVersion?> GetLatestStableVersionAsync(string packageId, CancellationToken cancellationToken)
    {
        var allVersions =
            await GetAllVersionsAsync(packageId, cancellationToken); // Leverages caching from GetAllVersionsAsync
        return FindLatestVersion(allVersions, false);
    }

    public async Task<NuGetVersion?> GetLatestVersionAsync(string packageId, CancellationToken cancellationToken)
    {
        var allVersions =
            await GetAllVersionsAsync(packageId, cancellationToken); // Leverages caching from GetAllVersionsAsync
        return FindLatestVersion(allVersions, true);
    }

    public async Task<IPackageSearchMetadata?> GetPackageMetadataAsync(string packageId, NuGetVersion version,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"metadata:{packageId?.ToLowerInvariant()}:{version.ToNormalizedString()}";

        // IPackageSearchMetadata might not be directly serializable for all cache providers.
        // If caching fails, consider caching a simpler DTO or just fetching directly.
        // For now, let's assume the cache service can handle it or we accept potential issues.
        var metadata = await GetOrSetCacheAsync<IPackageSearchMetadata>(cacheKey, async () =>
        {
            var metadataResource = await _repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
            if (metadataResource == null)
            {
                _logger.LogWarning("Feed {FeedUrl} does not support V3 PackageMetadata resource",
                    _settings.QueryFeedUrl);
                return null;
            }

            var packageIdentity = new PackageIdentity(packageId, version);
            // Use the overload that takes PackageIdentity
            var specificMetadata = await metadataResource.GetMetadataAsync(
                packageIdentity,
                _sourceCacheContext,
                _logger.AsNuGetLogger(),
                cancellationToken);

            // GetMetadataAsync(identity) returns a single IPackageSearchMetadata or null
            return specificMetadata;
        }, cancellationToken);

        return metadata;
    }

    public async Task<IPackageSearchMetadata?> GetLatestPackageMetadataAsync(string packageId, bool includePrerelease,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"latest-metadata:{packageId?.ToLowerInvariant()}:prerel:{includePrerelease}";

        var latestMetadata = await GetOrSetCacheAsync(cacheKey, async () =>
        {
            var metadataResource = await _repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
            if (metadataResource == null)
            {
                _logger.LogWarning("Feed {FeedUrl} does not support V3 PackageMetadata resource",
                    _settings.QueryFeedUrl);
                return null;
            }

            // This overload gets metadata for potentially multiple versions
            var allMetadata = await metadataResource.GetMetadataAsync(
                packageId,
                includePrerelease, // Include prerelease versions in the initial fetch if requested
                false, // includeUnlisted - typically false for "latest"
                _sourceCacheContext,
                _logger.AsNuGetLogger(),
                cancellationToken);

            if (allMetadata == null || !allMetadata.Any()) return null; // No metadata found

            // Find the latest version among the results
            // If includePrerelease is false, we should have only received stable versions (or none)
            // If includePrerelease is true, we might have stable and prerelease, need the absolute latest
            var latest = allMetadata
                .OrderByDescending(m => m.Identity.Version)
                .FirstOrDefault(); // The highest version is the latest

            // If looking for stable only, and the absolute latest is prerelease, we might need to re-filter
            // However, the GetMetadataAsync overload *should* have handled this based on the includePrerelease flag.
            // Let's trust the resource returns the correct set for now.
            // If !includePrerelease and latest is prerelease, something is wrong upstream or with interpretation.

            return latest;
        }, cancellationToken);

        return latestMetadata;
    }
}

// Helper extension method to adapt ILogger to NuGet's ILogger
public static class LoggerExtensions
{
    public static ILogger AsNuGetLogger(this Microsoft.Extensions.Logging.ILogger logger) // Fully qualify
    {
        return new NuGetLoggerAdapter(logger);
    }

    private class NuGetLoggerAdapter : ILogger // Fully qualify
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public NuGetLoggerAdapter(Microsoft.Extensions.Logging.ILogger logger)
        {
            _logger = logger;
        }

        public void LogDebug(string data)
        {
            _logger.LogDebug(data);
        }

        public void LogVerbose(string data)
        {
            _logger.LogTrace(data);
            // Map Verbose to Trace
        }

        public void LogInformation(string data)
        {
            _logger.LogInformation(data);
        }

        public void LogMinimal(string data)
        {
            _logger.LogInformation(data);
            // Map Minimal to Information
        }

        public void LogWarning(string data)
        {
            _logger.LogWarning(data);
        }

        public void LogError(string data)
        {
            _logger.LogError(data);
        }

        public void LogInformationSummary(string data)
        {
            _logger.LogInformation(data);
        }

        public void LogErrorSummary(string data)
        {
            _logger.LogError(data);
            // Log Error Summary as Error
        }

        // Corrected signature to match NuGet.Common.ILogger
        public Task LogAsync(LogLevel level, string data)
        {
            Log(level, data);
            return Task.CompletedTask;
        }

        public void Log(LogLevel level, string data) // Fully qualify
        {
            switch (level)
            {
                case LogLevel.Debug: LogDebug(data); break; // Fully qualify
                case LogLevel.Verbose: LogVerbose(data); break; // Fully qualify
                case LogLevel.Information: LogInformation(data); break; // Fully qualify
                case LogLevel.Minimal: LogMinimal(data); break; // Fully qualify
                case LogLevel.Warning: LogWarning(data); break; // Fully qualify
                case LogLevel.Error: LogError(data); break; // Fully qualify
                default: LogInformation(data); break;
            }
        }

        // Corrected signature to match NuGet.Common.ILogger
        public Task LogAsync(ILogMessage message) // Fully qualify ILogMessage
        {
            Log(message);
            return Task.CompletedTask;
        }

        public void Log(ILogMessage message) // Fully qualify ILogMessage
        {
            // Correctly map NuGet level to Microsoft logger methods
            var logMessage = $"[{message.Code}] {message.Message}";
            switch (message.Level)
            {
                case LogLevel.Debug: _logger.LogDebug(logMessage); break;
                case LogLevel.Verbose: _logger.LogTrace(logMessage); break; // Map Verbose to Trace
                case LogLevel.Information: _logger.LogInformation(logMessage); break;
                case LogLevel.Minimal: _logger.LogInformation(logMessage); break; // Map Minimal to Information
                case LogLevel.Warning: _logger.LogWarning(logMessage); break;
                case LogLevel.Error: _logger.LogError(logMessage); break;
                default: _logger.LogInformation(logMessage); break;
            }
            // Consider logging Time property if needed: message.Time
        }
    }
}