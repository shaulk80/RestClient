using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

public interface ICollectionRepository
{
    Task<Result<IReadOnlyList<Collection>>> LoadAsync(CancellationToken cancellationToken);

    Task<Result> SaveAsync(IReadOnlyList<Collection> collections, CancellationToken cancellationToken);
}
