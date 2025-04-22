namespace NuGetContextMcpServer.Infrastructure.Configuration;

public class NuGetSettings
{
    // Set a default value to the official NuGet feed
    public string QueryFeedUrl { get; set; } = "https://api.nuget.org/v3/index.json";
    public string? Username { get; set; }
    public string? PasswordOrPat { get; set; }
}