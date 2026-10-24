using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Abstractions;

public interface ICollectionRepository
{
    Task<Result<IReadOnlyList<Collection>>> LoadAsync(CancellationToken cancellationToken);

    Task<Result> SaveAsync(IReadOnlyList<Collection> collections, CancellationToken cancellationToken);
}
