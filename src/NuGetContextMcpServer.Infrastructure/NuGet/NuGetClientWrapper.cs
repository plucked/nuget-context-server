using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
using NuGetContextMcpServer.Infrastructure.Configuration;
using NuGetContextMcpServer.Abstractions.Dtos; // Updated namespace
// Assuming DTOs like PackageSearchResult are defined in Application or a shared DTO project
// If they are elsewhere, adjust the using statement. For now, assume Application.Interfaces is sufficient
// or that they will be created later.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetContextMcpServer.Infrastructure.NuGet;

public class NuGetClientWrapper : INuGetQueryService
{
    private readonly NuGetSettings _settings;
    private readonly ICacheService _cacheService;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<NuGetClientWrapper> _logger;
    private readonly SourceRepository _repository;
    private readonly SourceCacheContext _sourceCacheContext; // NuGet's internal cache context

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

        // NuGet's SourceCacheContext helps with its internal HTTP caching etc.
        // Dispose this context? Seems okay to keep it for the service lifetime.
        _sourceCacheContext = new SourceCacheContext { NoCache = false }; // Enable NuGet's internal caching

        _repository = CreateSourceRepository();
    }

    private SourceRepository CreateSourceRepository()
    {
        // Ensure the URL points to the V3 index
        string feedUrl = _settings.QueryFeedUrl;
        if (!feedUrl.EndsWith("/v3/index.json", StringComparison.OrdinalIgnoreCase))
        {
            feedUrl = feedUrl.TrimEnd('/') + "/v3/index.json";
            _logger.LogDebug("Appending /v3/index.json to QueryFeedUrl for PackageSource: {FeedUrl}", feedUrl);
        }
        PackageSource packageSource = new PackageSource(feedUrl);


        if (!string.IsNullOrEmpty(_settings.PasswordOrPat))
        {
            // Use PAT as password. Username might be required or can be arbitrary.
            string username = string.IsNullOrEmpty(_settings.Username) ? "pat" : _settings.Username;
            packageSource.Credentials = new PackageSourceCredential(
                source: feedUrl, // Use the potentially modified feedUrl
                username: username,
                passwordText: _settings.PasswordOrPat,
                isPasswordClearText: true, // PATs are treated as clear text passwords
                validAuthenticationTypesText: null); // Let NuGet determine auth type
            _logger.LogInformation("Using configured credentials for feed {FeedUrl}", feedUrl); // Use modified feedUrl
        }
        else
        {
            _logger.LogInformation("No credentials configured for feed {FeedUrl}", feedUrl); // Use modified feedUrl
        }

        // Register providers (needed for core functionality)
        var providers = new List<Lazy<INuGetResourceProvider>>();
        providers.AddRange(Repository.Provider.GetCoreV3()); // Add default V3 providers

        return new SourceRepository(packageSource, providers);
    }

    // Helper to get data from cache or fetch from source
    private async Task<T?> GetOrSetCacheAsync<T>(
        string cacheKey,
        Func<Task<T?>> fetchFunc,
        CancellationToken cancellationToken) where T : class
    {
        var cachedItem = await _cacheService.GetAsync<T>(cacheKey, cancellationToken);
        if (cachedItem != null)
        {
            return cachedItem;
        }

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
    public async Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease, int skip, int take, CancellationToken cancellationToken)
    {
        // Include skip and take in the cache key for proper pagination caching
        string cacheKey = $"search:{searchTerm?.ToLowerInvariant()}:prerel:{includePrerelease}:skip:{skip}:take:{take}";

        // Cache the list of results for this specific page
        var results = await GetOrSetCacheAsync<List<PackageSearchResult>>(cacheKey, async () =>
        {
            var searchResource = await _repository.GetResourceAsync<PackageSearchResource>(cancellationToken);
            if (searchResource == null)
            {
                _logger.LogWarning("Feed {FeedUrl} does not support V3 Search resource.", _settings.QueryFeedUrl);
                return null; // Return null to indicate fetch failure
            }

            SearchFilter searchFilter = new SearchFilter(includePrerelease: includePrerelease);
            var searchResults = await searchResource.SearchAsync(
                searchTerm,
                searchFilter,
                skip: skip, // Use the passed skip parameter
                take: take, // Use the passed take parameter
                _logger.AsNuGetLogger(),
                cancellationToken);

            // Map to DTO before caching
            return searchResults?.Select(r => new PackageSearchResult(
                                        r.Identity.Id,
                                        r.Identity.Version.ToNormalizedString(),
                                        r.Description,
                                        r.ProjectUrl?.ToString()
                                    )).ToList(); // Cache as List<T>

        }, cancellationToken);

        return results ?? Enumerable.Empty<PackageSearchResult>(); // Return empty if fetch failed or cache empty
    }


    public async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string packageId, CancellationToken cancellationToken)
    {
         // Note: Caching raw NuGetVersion list might require custom converter or caching string list
         // For simplicity, let's cache the string list.
        string cacheKey = $"versions:{packageId?.ToLowerInvariant()}";

        var versionStrings = await GetOrSetCacheAsync<List<string>>(cacheKey, async () =>
        {
            var findPackageResource = await _repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
            if (findPackageResource == null)
            {
                 _logger.LogWarning("Feed {FeedUrl} does not support V3 FindPackageById resource.", _settings.QueryFeedUrl);
                return null;
            }

            var versions = await findPackageResource.GetAllVersionsAsync(
                packageId,
                _sourceCacheContext,
                _logger.AsNuGetLogger(),
                cancellationToken);

            return versions?.Select(v => v.ToNormalizedString()).ToList();

        }, cancellationToken);

        // Convert back to NuGetVersion after retrieving from cache
        return versionStrings?.Select(NuGetVersion.Parse) ?? Enumerable.Empty<NuGetVersion>();
    }

    // Helper to find latest based on cached/fetched versions
    private NuGetVersion? FindLatestVersion(IEnumerable<NuGetVersion>? versions, bool includePrerelease)
    {
        if (versions == null || !versions.Any()) return null;

        if (includePrerelease)
        {
            // Find absolute latest
            return versions.OrderByDescending(v => v).FirstOrDefault();
        }
        else
        {
            // Find latest stable
            return versions.Where(v => !v.IsPrerelease).OrderByDescending(v => v).FirstOrDefault();
        }
    }

    public async Task<NuGetVersion?> GetLatestStableVersionAsync(string packageId, CancellationToken cancellationToken)
    {
        var allVersions = await GetAllVersionsAsync(packageId, cancellationToken); // Leverages caching from GetAllVersionsAsync
        return FindLatestVersion(allVersions, includePrerelease: false);
    }

    public async Task<NuGetVersion?> GetLatestVersionAsync(string packageId, CancellationToken cancellationToken)
    {
         var allVersions = await GetAllVersionsAsync(packageId, cancellationToken); // Leverages caching from GetAllVersionsAsync
        return FindLatestVersion(allVersions, includePrerelease: true);
    }
}

// Helper extension method to adapt ILogger to NuGet's ILogger
public static class LoggerExtensions
{
    public static global::NuGet.Common.ILogger AsNuGetLogger(this Microsoft.Extensions.Logging.ILogger logger) // Fully qualify
    {
        return new NuGetLoggerAdapter(logger);
    }

    private class NuGetLoggerAdapter : global::NuGet.Common.ILogger // Fully qualify
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public NuGetLoggerAdapter(Microsoft.Extensions.Logging.ILogger logger)
        {
            _logger = logger;
        }

        public void LogDebug(string data) => _logger.LogDebug(data);
        public void LogVerbose(string data) => _logger.LogTrace(data); // Map Verbose to Trace
        public void LogInformation(string data) => _logger.LogInformation(data);
        public void LogMinimal(string data) => _logger.LogInformation(data); // Map Minimal to Information
        public void LogWarning(string data) => _logger.LogWarning(data);
        public void LogError(string data) => _logger.LogError(data);
        public void LogInformationSummary(string data) => _logger.LogInformation(data);
        public void LogErrorSummary(string data) => _logger.LogError(data); // Log Error Summary as Error

        // Corrected signature to match NuGet.Common.ILogger
        public Task LogAsync(global::NuGet.Common.LogLevel level, string data)
        {
            Log(level, data);
            return Task.CompletedTask;
        }

        public void Log(global::NuGet.Common.LogLevel level, string data) // Fully qualify
        {
            switch (level)
            {
                case global::NuGet.Common.LogLevel.Debug: LogDebug(data); break; // Fully qualify
                case global::NuGet.Common.LogLevel.Verbose: LogVerbose(data); break; // Fully qualify
                case global::NuGet.Common.LogLevel.Information: LogInformation(data); break; // Fully qualify
                case global::NuGet.Common.LogLevel.Minimal: LogMinimal(data); break; // Fully qualify
                case global::NuGet.Common.LogLevel.Warning: LogWarning(data); break; // Fully qualify
                case global::NuGet.Common.LogLevel.Error: LogError(data); break; // Fully qualify
                default: LogInformation(data); break;
            }
        }

        // Corrected signature to match NuGet.Common.ILogger
        public Task LogAsync(global::NuGet.Common.ILogMessage message) // Fully qualify ILogMessage
        {
             Log(message);
             return Task.CompletedTask;
        }

         public void Log(global::NuGet.Common.ILogMessage message) // Fully qualify ILogMessage
        {
             // Correctly map NuGet level to Microsoft logger methods
             var logMessage = $"[{message.Code}] {message.Message}";
             switch(message.Level)
             {
                 case global::NuGet.Common.LogLevel.Debug: _logger.LogDebug(logMessage); break;
                 case global::NuGet.Common.LogLevel.Verbose: _logger.LogTrace(logMessage); break; // Map Verbose to Trace
                 case global::NuGet.Common.LogLevel.Information: _logger.LogInformation(logMessage); break;
                 case global::NuGet.Common.LogLevel.Minimal: _logger.LogInformation(logMessage); break; // Map Minimal to Information
                 case global::NuGet.Common.LogLevel.Warning: _logger.LogWarning(logMessage); break;
                 case global::NuGet.Common.LogLevel.Error: _logger.LogError(logMessage); break;
                 default: _logger.LogInformation(logMessage); break;
             }
             // Consider logging Time property if needed: message.Time
        }
    }
}