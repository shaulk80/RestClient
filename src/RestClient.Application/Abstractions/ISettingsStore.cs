using RestClient.Domain.Results;

namespace RestClient.Application.Abstractions;

// Plain key-value settings. A missing key is a success with a null value.
public interface ISettingsStore
{
    Task<Result<string?>> GetAsync(string key, CancellationToken cancellationToken);

    Task<Result> SetAsync(string key, string value, CancellationToken cancellationToken);
}
