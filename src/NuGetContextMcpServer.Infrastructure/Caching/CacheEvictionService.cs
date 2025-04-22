using Microsoft.Extensions.DependencyInjection; // Added for CreateScope
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace
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