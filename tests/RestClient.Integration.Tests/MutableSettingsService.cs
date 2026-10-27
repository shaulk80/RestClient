using RestClient.Application.Settings;
using RestClient.Domain.Results;

namespace RestClient.Integration.Tests;

// Settings that a test can change between sends, like the settings window does.
public sealed class MutableSettingsService : ISettingsService
{
    public MutableSettingsService(AppSettings current)
    {
        Current = current;
    }

    public AppSettings Current { get; set; }

    public Task<Result<AppSettings>> GetAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<AppSettings>.Success(Current));
    }

    public Task<Result> SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Current = settings;
        return Task.FromResult(Result.Success());
    }
}
