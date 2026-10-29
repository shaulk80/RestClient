using FakeItEasy;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Workspaces;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.Collections;

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
