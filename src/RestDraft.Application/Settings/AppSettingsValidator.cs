using RestDraft.Domain.Results;

namespace RestDraft.Application.Settings;

public static class AppSettingsValidator
{
    public const int MaxTimeoutSeconds = 3600;
    public const int MaxRedirectLimit = 50;
    public const int MinPanelWidth = 100;
    public const int MaxPanelWidth = 4000;

    private static readonly string[] ProxySchemes = ["http", "https", "socks4", "socks4a", "socks5"];

    public static Result Validate(AppSettings settings)
    {
        if (settings.TimeoutSeconds is < 1 or > MaxTimeoutSeconds)
        {
            return Invalid($"The timeout must be between 1 and {MaxTimeoutSeconds} seconds.");
        }

        if (settings.MaxRedirects is < 1 or > MaxRedirectLimit)
        {
            return Invalid($"The redirect limit must be between 1 and {MaxRedirectLimit}.");
        }

        if (!Enum.IsDefined(settings.Theme))
        {
            return Invalid("The theme must be System, Light or Dark.");
        }

        if (settings.SidebarWidth is < MinPanelWidth or > MaxPanelWidth || settings.ResponseWidth is < MinPanelWidth or > MaxPanelWidth)
        {
            return Invalid($"A panel width must be between {MinPanelWidth} and {MaxPanelWidth} pixels.");
        }

        return ValidateProxy(settings.ProxyUrl);
    }

    private static Result ValidateProxy(string proxyUrl)
    {
        if (proxyUrl.Length == 0)
        {
            return Result.Success();
        }

        var valid = Uri.TryCreate(proxyUrl, UriKind.Absolute, out var uri)
            && ProxySchemes.Contains(uri.Scheme)
            && uri.Host.Length > 0;
        return valid
            ? Result.Success()
            : Invalid("The proxy must be an absolute URL such as http://proxy:8080, or empty for the system proxy.");
    }

    private static Result Invalid(string message)
    {
        return Result.Failure(ResultErrors.Validation(message));
    }
}
