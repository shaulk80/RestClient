using RestClient.Application.Abstractions;
using RestClient.Domain.Environments;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Environments;

internal sealed class EnvironmentRepositoryFake : IEnvironmentRepository
{
    public List<RequestEnvironment> Stored { get; set; } = [];

    public int SaveCount { get; private set; }

    public Task<Result<IReadOnlyList<RequestEnvironment>>> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<IReadOnlyList<RequestEnvironment>>.Success(Stored.ToList()));
    }

    public Task<Result> SaveAsync(IReadOnlyList<RequestEnvironment> environments, CancellationToken cancellationToken)
    {
        SaveCount++;
        Stored = environments.ToList();
        return Task.FromResult(Result.Success());
    }
}
