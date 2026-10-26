using RestClient.Domain.Environments;
using RestClient.Domain.Results;

namespace RestClient.Application.Environments;

public interface IEnvironmentService
{
    Task<Result<IReadOnlyList<RequestEnvironment>>> ListAsync(CancellationToken cancellationToken);

    // Success with a null value means no environment is active.
    Task<Result<RequestEnvironment?>> GetActiveAsync(CancellationToken cancellationToken);

    Task<Result> SetActiveAsync(Guid? environmentId, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken);

    Task<Result> RenameAsync(Guid environmentId, string name, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid environmentId, CancellationToken cancellationToken);

    Task<Result> UpdateVariablesAsync(Guid environmentId, IReadOnlyList<VariableEdit> edits, CancellationToken cancellationToken);
}
