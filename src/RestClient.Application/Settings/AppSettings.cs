namespace RestClient.Application.Settings;

// Application settings. They apply to the next send or the next click, so no restart is needed.
public sealed record AppSettings
{
    public static readonly AppSettings Default = new();

    public int TimeoutSeconds { get; init; } = 30;

    public bool FollowRedirects { get; init; } = true;

    public int MaxRedirects { get; init; } = 10;

    public bool VerifySsl { get; init; } = true;

    // Empty means the system proxy.
    public string ProxyUrl { get; init; } = string.Empty;

    // The Collections tree opens a request on a single click. Off means a double-click.
    public bool OpenOnSingleClick { get; init; }

    // Panel widths in pixels. Remembered when a splitter is dragged.
    public int SidebarWidth { get; init; } = 260;

    public int ResponseWidth { get; init; } = 420;

    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);
}
