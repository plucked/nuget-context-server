using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
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
            using var transaction = (SqliteTransaction)await _connection.BeginTransactionAsync(cancellationToken); // Explicit cast
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
         _logger.LogError("SQLite command failed after {MaxRetryAttempts} attempts due to BUSY/LOCKED errors.", MaxRetryAttempts);
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