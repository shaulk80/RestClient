using FakeItEasy;
using RestClient.Application.Abstractions;
using RestClient.Application.Workspaces;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Collections;

internal static class CollectionRepositoryFake
{
    public static Workspace<Collection> CreateWorkspace(CollectionStore store)
    {
        var repository = Create(store);
        return new Workspace<Collection>(repository.LoadAsync, repository.SaveAsync);
    }

    public static ICollectionRepository Create(CollectionStore store)
    {
        var repository = A.Fake<ICollectionRepository>();
        A.CallTo(() => repository.LoadAsync(A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult(Result<IReadOnlyList<Collection>>.Success(store.Collections)));
        A.CallTo(() => repository.SaveAsync(A<IReadOnlyList<Collection>>._, A<CancellationToken>._))
            .ReturnsLazily((IReadOnlyList<Collection> collections, CancellationToken _) =>
            {
                store.Collections = collections;
                return Task.FromResult(Result.Success());
            });
        return repository;
    }
}
