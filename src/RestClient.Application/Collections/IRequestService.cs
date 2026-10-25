using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Collections;

public interface IRequestService
{
    Task<Result<RequestDefinition>> GetAsync(Guid requestId, CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(Guid folderId, CancellationToken cancellationToken);

    Task<Result> RenameAsync(Guid requestId, string name, CancellationToken cancellationToken);

    Task<Result> UpdateAsync(Guid requestId, Func<RequestDefinition, RequestDefinition> change, CancellationToken cancellationToken);

    Task<Result<Guid>> DuplicateAsync(Guid requestId, CancellationToken cancellationToken);

    Task<Result<Guid>> AddCopyAsync(Guid folderId, RequestDefinition source, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid requestId, CancellationToken cancellationToken);
}
