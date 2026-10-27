namespace RestClient.Application.Settings;

// Transport settings. They apply to the next send, so no restart is needed.
public sealed record AppSettings
{
    public static readonly AppSettings Default = new();

    public int TimeoutSeconds { get; init; } = 30;

    public bool FollowRedirects { get; init; } = true;

    public int MaxRedirects { get; init; } = 10;

    public bool VerifySsl { get; init; } = true;

    // Empty means the system proxy.
    public string ProxyUrl { get; init; } = string.Empty;

    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);
}
