using RestDraft.Domain.Environments;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

public interface IEnvironmentRepository
{
    Task<Result<IReadOnlyList<RequestEnvironment>>> LoadAsync(CancellationToken cancellationToken);

    Task<Result> SaveAsync(IReadOnlyList<RequestEnvironment> environments, CancellationToken cancellationToken);
}
