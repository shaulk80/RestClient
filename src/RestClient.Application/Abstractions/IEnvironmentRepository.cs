using RestClient.Domain.Environments;
using RestClient.Domain.Results;

namespace RestClient.Application.Abstractions;

public interface IEnvironmentRepository
{
    Task<Result<IReadOnlyList<RequestEnvironment>>> LoadAsync(CancellationToken cancellationToken);

    Task<Result> SaveAsync(IReadOnlyList<RequestEnvironment> environments, CancellationToken cancellationToken);
}
