using RestDraft.Application.Settings;

namespace RestDraft.Infrastructure.Http;

public sealed record HttpTransportOptions(bool FollowRedirects, int MaxRedirects, bool VerifySsl = true, string ProxyUrl = "")
{
    public static HttpTransportOptions From(AppSettings settings)
    {
        return new HttpTransportOptions(settings.FollowRedirects, settings.MaxRedirects, settings.VerifySsl, settings.ProxyUrl);
    }
}
