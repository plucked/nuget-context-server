using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetContextMcpServer.Abstractions.Interfaces; // Updated namespace

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan absoluteExpirationRelativeToNow, CancellationToken cancellationToken) where T : class;
    Task RemoveAsync(string key, CancellationToken cancellationToken);
    Task RemoveExpiredAsync(CancellationToken cancellationToken); // For eviction service
}