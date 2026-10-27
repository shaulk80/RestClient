using RestClient.Application.Settings;

namespace RestClient.Infrastructure.Http;

public sealed record HttpTransportOptions(bool FollowRedirects, int MaxRedirects, bool VerifySsl = true, string ProxyUrl = "")
{
    public static HttpTransportOptions From(AppSettings settings)
    {
        return new HttpTransportOptions(settings.FollowRedirects, settings.MaxRedirects, settings.VerifySsl, settings.ProxyUrl);
    }
}
