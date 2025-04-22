# Task 03: Implement Infrastructure Services (Parsing, Caching, NuGet)

**Goal:** Implement the concrete classes for parsing solution/project files, caching data using SQLite, and interacting with the NuGet API.

**Outcome:** Functional implementations of `MsBuildSolutionParser`, `MsBuildProjectParser`, `SqliteCacheService`, and `NuGetClientWrapper` exist in the `Infrastructure` project, ready to be registered in DI and used by the application layer.

---

## Sub-Tasks:

### 3.1 Implement `MsBuildSolutionParser` (`Infrastructure` Project)
*   **Action:** Implement the `MsBuildSolutionParser` class in the `Parsing` folder, inheriting from `ISolutionParser`. Use `Microsoft.Build.Construction.SolutionFile`.
*   **File Content (`MsBuildSolutionParser.cs`):**
    ```csharp
    using Microsoft.Build.Construction;
    using Microsoft.Extensions.Logging;
    using NuGetContextMcpServer.Application.Interfaces;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    namespace NuGetContextMcpServer.Infrastructure.Parsing;

    public class MsBuildSolutionParser : ISolutionParser
    {
        private readonly ILogger<MsBuildSolutionParser> _logger;

        public MsBuildSolutionParser(ILogger<MsBuildSolutionParser> logger)
        {
            _logger = logger;
        }

        public Task<IEnumerable<string>> GetProjectPathsAsync(string solutionPath, CancellationToken cancellationToken)
        {
            // Ensure MSBuild is registered (should have happened at startup)
            // MsBuildInitializer.EnsureMsBuildRegistered(); // Already called in Program.cs

            return Task.Run(() => // Run potentially blocking file I/O and parsing off the main thread if needed
            {
                List<string> projectPaths = new();
                try
                {
                    if (!File.Exists(solutionPath))
                    {
                        _logger.LogError("Solution file not found at {Path}", solutionPath);
                        return Enumerable.Empty<string>();
                    }

                    _logger.LogDebug("Parsing solution file: {Path}", solutionPath);
                    SolutionFile solutionFile = SolutionFile.Parse(solutionPath);

                    foreach (ProjectInSolution projectInSolution in solutionFile.ProjectsInOrder)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Filter out solution folders, non-MSBuild projects, etc.
                        if (projectInSolution.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat &&
                            !string.IsNullOrEmpty(projectInSolution.AbsolutePath)) // Ensure path exists
                        {
                            // Check if the project file actually exists
                            if (File.Exists(projectInSolution.AbsolutePath))
                            {
                                projectPaths.Add(projectInSolution.AbsolutePath);
                                _logger.LogDebug("Found project {ProjectName} at {Path}", projectInSolution.ProjectName, projectInSolution.AbsolutePath);
                            }
                            else
                            {
                                _logger.LogWarning("Project '{ProjectName}' listed in solution but not found at expected path: {Path}",
                                    projectInSolution.ProjectName, projectInSolution.AbsolutePath);
                            }
                        }
                    }
                    _logger.LogInformation("Parsed {Count} valid project paths from solution {Path}", projectPaths.Count, solutionPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing solution file {Path}", solutionPath);
                    // Depending on requirements, might rethrow or return empty/partial list
                    return Enumerable.Empty<string>();
                }
                return projectPaths.AsEnumerable();
            }, cancellationToken);
        }
    }
    ```
*   **Outcome:** `MsBuildSolutionParser.cs` implemented.

### 3.2 Implement `MsBuildProjectParser` (`Infrastructure` Project)
*   **Action:** Implement the `MsBuildProjectParser` class in the `Parsing` folder, inheriting from `IProjectParser`. Use `Microsoft.Build.Evaluation.Project`.
*   **File Content (`MsBuildProjectParser.cs`):**
    ```csharp
    using Microsoft.Build.Evaluation;
    using Microsoft.Extensions.Logging;
    using NuGetContextMcpServer.Application.Interfaces;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    namespace NuGetContextMcpServer.Infrastructure.Parsing;

    public class MsBuildProjectParser : IProjectParser
    {
        private readonly ILogger<MsBuildProjectParser> _logger;
        // Consider ProjectCollection lifetime management if not using GlobalProjectCollection
        private readonly ProjectCollection _projectCollection;

        public MsBuildProjectParser(ILogger<MsBuildProjectParser> logger)
        {
            _logger = logger;
            // Using GlobalProjectCollection is simpler but loads projects into a shared space.
            // A dedicated ProjectCollection might offer better isolation if needed.
            _projectCollection = ProjectCollection.GlobalProjectCollection;
        }

        public Task<IEnumerable<ParsedPackageReference>> GetPackageReferencesAsync(string projectPath, CancellationToken cancellationToken)
        {
            // Ensure MSBuild is registered (should have happened at startup)
            // MsBuildInitializer.EnsureMsBuildRegistered(); // Already called in Program.cs

            return Task.Run(() => // Run potentially blocking evaluation off the main thread
            {
                List<ParsedPackageReference> references = new();
                Project? project = null;
                try
                {
                    if (!File.Exists(projectPath))
                    {
                        _logger.LogError("Project file not found at {Path}", projectPath);
                        return Enumerable.Empty<ParsedPackageReference>();
                    }

                    _logger.LogDebug("Loading and evaluating project: {Path}", projectPath);

                    // Load and evaluate the project. Pass null for global properties initially.
                    // Consider if specific configurations (Debug/Release) are needed.
                    project = _projectCollection.LoadProject(projectPath, null, null);

                    // Extract PackageReferences using the evaluated items
                    var packageReferenceItems = project.GetItems("PackageReference");

                    foreach (var item in packageReferenceItems)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string packageId = item.EvaluatedInclude;
                        // Use GetMetadataValue which respects evaluation (conditions, CPM etc.)
                        string packageVersion = item.GetMetadataValue("Version");

                        if (!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(packageVersion))
                        {
                            references.Add(new ParsedPackageReference(packageId, packageVersion));
                            _logger.LogDebug("Found PackageReference: {Id} Version: {Version}", packageId, packageVersion);
                        }
                        else
                        {
                             _logger.LogWarning("Found PackageReference with missing ID or Version in project {Path}: Include='{Include}'", projectPath, packageId);
                        }
                    }
                    _logger.LogInformation("Parsed {Count} PackageReferences from project {Path}", references.Count, projectPath);
                }
                catch (Microsoft.Build.Exceptions.InvalidProjectFileException ex)
                {
                    _logger.LogError(ex, "Invalid project file format for {Path}: {Message}", projectPath, ex.BaseMessage);
                    return Enumerable.Empty<ParsedPackageReference>(); // Or throw specific exception
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing project file {Path}", projectPath);
                    return Enumerable.Empty<ParsedPackageReference>(); // Or throw
                }
                finally
                {
                    // If using a dedicated ProjectCollection, unload the project
                    // if (project != null && _projectCollection != ProjectCollection.GlobalProjectCollection)
                    // {
                    //     _projectCollection.UnloadProject(project);
                    //     _logger.LogDebug("Unloaded project: {Path}", projectPath);
                    // }
                }
                return references.AsEnumerable();
            }, cancellationToken);
        }
    }
    ```
*   **Outcome:** `MsBuildProjectParser.cs` implemented.

### 3.3 Implement `SqliteCacheService` (`Infrastructure` Project)
*   **Action:** Create a `Caching` folder. Implement `SqliteCacheService` inheriting from `ICacheService`. Use `Microsoft.Data.Sqlite` and handle JSON serialization.
*   **File Content (`SqliteCacheService.cs`):**
    ```csharp
    using Microsoft.Data.Sqlite;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NuGetContextMcpServer.Application.Interfaces;
    using NuGetContextMcpServer.Infrastructure.Configuration;
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    namespace NuGetContextMcpServer.Infrastructure.Caching;

    public class SqliteCacheService : ICacheService, IDisposable
    {
        private readonly ILogger<SqliteCacheService> _logger;
        private readonly CacheSettings _cacheSettings;
        private readonly SqliteConnection _connection;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed = false;

        private const string TableName = "CacheEntries";
        private const int MaxRetryAttempts = 3;
        private const int RetryDelayMs = 100;

        public SqliteCacheService(IOptions<CacheSettings> cacheSettings, ILogger<SqliteCacheService> logger)
        {
            _logger = logger;
            _cacheSettings = cacheSettings.Value;

            // Ensure directory exists if DatabasePath includes folders
            var dbDir = Path.GetDirectoryName(_cacheSettings.DatabasePath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
                _logger.LogInformation("Created cache directory: {Directory}", dbDir);
            }

            var connectionString = $"Data Source={_cacheSettings.DatabasePath};"; // Basic connection string
            _connection = new SqliteConnection(connectionString);
            _connection.Open(); // Keep connection open for the lifetime of the service

            _logger.LogInformation("Opened SQLite connection to {DatabasePath}", _cacheSettings.DatabasePath);

            InitializeDatabase();

            // Configure JSON options (consider matching ASP.NET Core defaults if needed)
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // Add other options as required
            };
        }

        private void InitializeDatabase()
        {
            using var command = _connection.CreateCommand();

            // Enable WAL mode for better concurrency
            command.CommandText = "PRAGMA journal_mode=WAL;";
            try
            {
                command.ExecuteNonQuery();
                _logger.LogDebug("Enabled WAL mode for SQLite cache.");
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to enable WAL mode."); } // Log but continue

            // Consider synchronous mode for performance vs durability trade-off
            command.CommandText = "PRAGMA synchronous=NORMAL;"; // Faster than FULL, safer than OFF
             try
            {
                command.ExecuteNonQuery();
                _logger.LogDebug("Set synchronous=NORMAL for SQLite cache.");
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to set synchronous mode."); }

            // Create cache table if it doesn't exist
            command.CommandText = $@"
                CREATE TABLE IF NOT EXISTS {TableName} (
                    CacheKey TEXT PRIMARY KEY,
                    ResponseJson TEXT NOT NULL,
                    ExpirationTimestamp INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_{TableName}_ExpirationTimestamp ON {TableName}(ExpirationTimestamp);
            ";
            command.ExecuteNonQuery();
            _logger.LogInformation("Initialized SQLite cache database schema.");
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
        {
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string? json = null;

            await ExecuteWithRetryAsync(async (cmd) =>
            {
                cmd.CommandText = $@"
                    SELECT ResponseJson
                    FROM {TableName}
                    WHERE CacheKey = @key AND ExpirationTimestamp > @now";
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@now", nowUnix);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    json = reader.GetString(0);
                }
            }, cancellationToken);


            if (json != null)
            {
                try
                {
                    var value = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return value;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize cached JSON for key: {Key}. Removing invalid entry.", key);
                    // Attempt to remove the corrupted entry
                    await RemoveAsync(key, CancellationToken.None); // Use separate token if needed
                    return null;
                }
            }
            else
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan absoluteExpirationRelativeToNow, CancellationToken cancellationToken) where T : class
        {
            string json = JsonSerializer.Serialize(value, _jsonOptions);
            long expirationUnix = DateTimeOffset.UtcNow.Add(absoluteExpirationRelativeToNow).ToUnixTimeSeconds();

            await ExecuteWithRetryAsync(async (cmd) =>
            {
                // Use INSERT OR REPLACE (UPSERT)
                cmd.CommandText = $@"
                    INSERT OR REPLACE INTO {TableName} (CacheKey, ResponseJson, ExpirationTimestamp)
                    VALUES (@key, @json, @expiration)";
                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@json", json);
                cmd.Parameters.AddWithValue("@expiration", expirationUnix);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogDebug("Set cache entry for key: {Key}", key);

            }, cancellationToken);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
             await ExecuteWithRetryAsync(async (cmd) =>
            {
                cmd.CommandText = $"DELETE FROM {TableName} WHERE CacheKey = @key";
                cmd.Parameters.AddWithValue("@key", key);
                int rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
                 _logger.LogDebug("Removed cache entry for key: {Key} (Rows affected: {Rows})", key, rowsAffected);
            }, cancellationToken);
        }

        public async Task RemoveExpiredAsync(CancellationToken cancellationToken)
        {
            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int rowsAffected = 0;

            await ExecuteWithRetryAsync(async (cmd) =>
            {
                 // Wrap deletion in a transaction for potentially better performance if many rows are deleted
                using var transaction = await _connection.BeginTransactionAsync(cancellationToken);
                cmd.Transaction = transaction; // Associate command with transaction
                cmd.CommandText = $"DELETE FROM {TableName} WHERE ExpirationTimestamp <= @now";
                cmd.Parameters.AddWithValue("@now", nowUnix);
                rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

            }, cancellationToken);

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Removed {Count} expired cache entries.", rowsAffected);
                // Optionally run VACUUM to shrink DB file, but can be slow and blocking
                // await ExecuteWithRetryAsync(async (cmd) => { cmd.CommandText = "VACUUM;"; await cmd.ExecuteNonQueryAsync(cancellationToken); }, cancellationToken);
            }
            else
            {
                 _logger.LogDebug("No expired cache entries found to remove.");
            }
        }

        // Helper to handle SQLite BUSY/LOCKED errors with retry
        private async Task ExecuteWithRetryAsync(Func<SqliteCommand, Task> action, CancellationToken cancellationToken)
        {
            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    using var command = _connection.CreateCommand();
                    await action(command);
                    return; // Success
                }
                catch (SqliteException ex) when ((ex.SqliteErrorCode == 5 /* SQLITE_BUSY */ || ex.SqliteErrorCode == 6 /* SQLITE_LOCKED */) && attempt < MaxRetryAttempts)
                {
                    _logger.LogWarning(ex, "SQLite BUSY/LOCKED error on attempt {Attempt}/{MaxAttempts}. Retrying after delay...", attempt, MaxRetryAttempts);
                    await Task.Delay(RetryDelayMs * attempt, cancellationToken); // Exponential backoff could be better
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Error executing SQLite command.");
                     throw; // Rethrow other exceptions
                }
            }
             _logger.LogError("SQLite command failed after {MaxAttempts} attempts due to BUSY/LOCKED errors.", MaxRetryAttempts);
             // Optionally throw a specific exception indicating persistent contention
             throw new Exception($"SQLite operation failed after {MaxRetryAttempts} attempts due to contention.");
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _connection?.Close(); // Close connection on dispose
                    _connection?.Dispose();
                    _logger.LogInformation("Closed SQLite connection.");
                }
                _disposed = true;
            }
        }
    }
    ```
*   **Outcome:** `SqliteCacheService.cs` implemented with core caching logic, table initialization, WAL/sync pragmas, and basic retry for busy/locked errors.

### 3.4 Implement `NuGetClientWrapper` (`Infrastructure` Project)
*   **Action:** Create a `NuGet` folder. Implement `NuGetClientWrapper` inheriting from `INuGetQueryService`. Inject dependencies and integrate caching.
*   **File Content (`NuGetClientWrapper.cs`):**
    ```csharp
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NuGet.Common;
    using NuGet.Configuration;
    using NuGet.Protocol;
    using NuGet.Protocol.Core.Types;
    using NuGet.Versioning;
    using NuGetContextMcpServer.Application.Interfaces;
    using NuGetContextMcpServer.Infrastructure.Configuration;
    using NuGetContextMcpServer.Infrastructure.Mcp; // Assuming DTOs defined here or shared location
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
            PackageSource packageSource = new PackageSource(_settings.QueryFeedUrl);

            if (!string.IsNullOrEmpty(_settings.PasswordOrPat))
            {
                // Use PAT as password. Username might be required or can be arbitrary.
                string username = string.IsNullOrEmpty(_settings.Username) ? "pat" : _settings.Username;
                packageSource.Credentials = new PackageSourceCredential(
                    source: _settings.QueryFeedUrl,
                    username: username,
                    passwordText: _settings.PasswordOrPat,
                    isPasswordClearText: true, // PATs are treated as clear text passwords
                    validAuthenticationTypesText: null); // Let NuGet determine auth type
                _logger.LogInformation("Using configured credentials for feed {FeedUrl}", _settings.QueryFeedUrl);
            }
            else
            {
                _logger.LogInformation("No credentials configured for feed {FeedUrl}", _settings.QueryFeedUrl);
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

        public async Task<IEnumerable<PackageSearchResult>> SearchPackagesAsync(string searchTerm, bool includePrerelease, CancellationToken cancellationToken)
        {
            string cacheKey = $"search:{searchTerm?.ToLowerInvariant()}:prerel:{includePrerelease}";
            int take = 50; // Limit search results

            // Cache the list of results
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
                    skip: 0,
                    take: take,
                    _logger.AsNuGetLogger(), // Use NuGet's logger interface
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
        public static NuGet.Common.ILogger AsNuGetLogger(this Microsoft.Extensions.Logging.ILogger logger)
        {
            return new NuGetLoggerAdapter(logger);
        }

        private class NuGetLoggerAdapter : NuGet.Common.ILogger
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

            public Task LogAsync(LogLevel level, string data, CancellationToken cancellationToken)
            {
                Log(level, data);
                return Task.CompletedTask;
            }

            public void Log(LogLevel level, string data)
            {
                switch (level)
                {
                    case LogLevel.Debug: LogDebug(data); break;
                    case LogLevel.Verbose: LogVerbose(data); break;
                    case LogLevel.Information: LogInformation(data); break;
                    case LogLevel.Minimal: LogMinimal(data); break;
                    case LogLevel.Warning: LogWarning(data); break;
                    case LogLevel.Error: LogError(data); break;
                    default: LogInformation(data); break;
                }
            }

            public Task LogAsync(ILogMessage message, CancellationToken cancellationToken)
            {
                 Log(message);
                 return Task.CompletedTask;
            }

             public void Log(ILogMessage message)
            {
                 Log(message.Level, $"[{message.Code}] {message.Message}");
                 // Consider logging Time property if needed
            }
        }
    }
    ```
*   **Outcome:** `NuGetClientWrapper.cs` implemented, handling repository creation, credentials, caching via `ICacheService`, and core NuGet API interactions. Includes logger adapter.

---