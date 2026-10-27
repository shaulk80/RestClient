using RestClient.Domain.Results;

namespace RestClient.Application.Settings;

public interface ISettingsService
{
    Task<Result<AppSettings>> GetAsync(CancellationToken cancellationToken);

    // Validates first. Invalid settings are not stored.
    Task<Result> SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
