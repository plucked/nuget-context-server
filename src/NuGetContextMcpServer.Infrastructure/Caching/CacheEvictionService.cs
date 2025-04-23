using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGetContextMcpServer.Abstractions.Interfaces;
using NuGetContextMcpServer.Infrastructure.Configuration;

namespace NuGetContextMcpServer.Infrastructure.Caching;

public class CacheEvictionService : IHostedService, IDisposable
{
    private readonly ILogger<CacheEvictionService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private Timer? _timer;
    private readonly TimeSpan _interval;

    public CacheEvictionService(
        IServiceProvider serviceProvider, 
        IOptions<CacheSettings> cacheSettings,
        ILogger<CacheEvictionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(5, cacheSettings.Value.DefaultExpirationMinutes / 2.0));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cache Eviction Service starting. Eviction interval: {Interval}", _interval);
        _timer = new Timer(DoWork, null, TimeSpan.FromSeconds(15), _interval);
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        _logger.LogDebug("Cache Eviction Service is running");

        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
                await cacheService.RemoveExpiredAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during cache eviction");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cache Eviction Service stopping");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}