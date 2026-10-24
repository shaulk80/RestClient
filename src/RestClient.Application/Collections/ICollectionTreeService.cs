using RestClient.Domain.Results;

namespace RestClient.Application.Collections;

public interface ICollectionTreeService
{
    Task<Result> MoveAsync(Guid nodeId, Guid targetFolderId, Guid? beforeNodeId, CancellationToken cancellationToken);
}
