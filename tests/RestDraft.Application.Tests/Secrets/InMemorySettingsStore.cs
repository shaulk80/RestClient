using RestDraft.Application.Abstractions;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.Secrets;

internal sealed class InMemorySettingsStore : ISettingsStore
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public Task<Result<string?>> GetAsync(string key, CancellationToken cancellationToken)
    {
        var value = Values.TryGetValue(key, out var stored) ? stored : null;
        return Task.FromResult(Result<string?>.Success(value));
    }

    public Task<Result> SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        Values[key] = value;
        return Task.FromResult(Result.Success());
    }
}
