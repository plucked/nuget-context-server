namespace NuGetContextMcpServer.Infrastructure.Configuration;

public class CacheSettings
{
    public string DatabasePath { get; set; } = "nuget_cache.db";
    public int DefaultExpirationMinutes { get; set; } = 60;
}