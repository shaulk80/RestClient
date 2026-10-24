using RestClient.Domain.Collections;

namespace RestClient.Application.Tests.Collections;

// Holds what the fake repository has saved, so tests can check the stored tree after each operation.
internal sealed class CollectionStore
{
    public CollectionStore(IReadOnlyList<Collection> collections)
    {
        Collections = collections;
    }

    public IReadOnlyList<Collection> Collections { get; set; }
}
