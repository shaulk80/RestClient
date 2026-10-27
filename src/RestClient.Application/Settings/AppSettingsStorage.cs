using System.Globalization;
using RestClient.Application.Abstractions;
using RestClient.Domain.Results;

namespace RestClient.Application.Settings;

// Maps AppSettings to keys of the settings table. A missing or unreadable value falls back to the default,
// so one bad row cannot stop requests from being sent.
public sealed class AppSettingsStorage
{
    private const string TimeoutKey = "http.timeoutSeconds";
    private const string FollowRedirectsKey = "http.followRedirects";
    private const string MaxRedirectsKey = "http.maxRedirects";
    private const string VerifySslKey = "http.verifySsl";
    private const string ProxyUrlKey = "http.proxyUrl";

    private readonly ISettingsStore _store;

    public AppSettingsStorage(ISettingsStore store)
    {
        _store = store;
    }

    public async Task<Result<AppSettings>> LoadAsync(CancellationToken cancellationToken)
    {
        var defaults = AppSettings.Default;
        var timeout = await ReadAsync(TimeoutKey, cancellationToken);
        var follow = await ReadAsync(FollowRedirectsKey, cancellationToken);
        var maxRedirects = await ReadAsync(MaxRedirectsKey, cancellationToken);
        var verify = await ReadAsync(VerifySslKey, cancellationToken);
        var proxy = await ReadAsync(ProxyUrlKey, cancellationToken);

        var failed = new[] { timeout, follow, maxRedirects, verify, proxy }.FirstOrDefault(read => !read.IsSuccess);
        if (failed is not null)
        {
            return Result<AppSettings>.Failure(failed.Error!);
        }

        return Result<AppSettings>.Success(new AppSettings
        {
            TimeoutSeconds = ParseInt(timeout.Value, defaults.TimeoutSeconds),
            FollowRedirects = ParseBool(follow.Value, defaults.FollowRedirects),
            MaxRedirects = ParseInt(maxRedirects.Value, defaults.MaxRedirects),
            VerifySsl = ParseBool(verify.Value, defaults.VerifySsl),
            ProxyUrl = proxy.Value ?? defaults.ProxyUrl,
        });
    }

    public async Task<Result> SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            [TimeoutKey] = settings.TimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            [FollowRedirectsKey] = settings.FollowRedirects.ToString(CultureInfo.InvariantCulture),
            [MaxRedirectsKey] = settings.MaxRedirects.ToString(CultureInfo.InvariantCulture),
            [VerifySslKey] = settings.VerifySsl.ToString(CultureInfo.InvariantCulture),
            [ProxyUrlKey] = settings.ProxyUrl,
        };

        foreach (var (key, value) in values)
        {
            var saved = await _store.SetAsync(key, value, cancellationToken);
            if (!saved.IsSuccess)
            {
                return saved;
            }
        }

        return Result.Success();
    }

    private static int ParseInt(string? text, int fallback)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static bool ParseBool(string? text, bool fallback)
    {
        return bool.TryParse(text, out var value) ? value : fallback;
    }

    private Task<Result<string?>> ReadAsync(string key, CancellationToken cancellationToken)
    {
        return _store.GetAsync(key, cancellationToken);
    }
}
