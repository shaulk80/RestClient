using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Collections;

public interface ICollectionService
{
    Task<Result<IReadOnlyList<Collection>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken);

    Task<Result> RenameAsync(Guid collectionId, string name, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid collectionId, CancellationToken cancellationToken);
}
