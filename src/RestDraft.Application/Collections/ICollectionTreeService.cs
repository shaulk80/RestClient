using RestDraft.Domain.Results;

namespace RestDraft.Application.Collections;

public interface ICollectionTreeService
{
    Task<Result> MoveAsync(Guid nodeId, Guid targetFolderId, Guid? beforeNodeId, CancellationToken cancellationToken);
}
