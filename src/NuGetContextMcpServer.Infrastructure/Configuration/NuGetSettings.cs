namespace NuGetContextMcpServer.Infrastructure.Configuration;

public class NuGetSettings
{
    public string QueryFeedUrl { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? PasswordOrPat { get; set; }
}