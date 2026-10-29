using RestDraft.Domain.Collections;

namespace RestDraft.Application.Collections;

public static class CollectionSet
{
    public static Collection? Find(IReadOnlyList<Collection> collections, Guid collectionId)
    {
        return collections.FirstOrDefault(collection => collection.Id == collectionId);
    }

    public static Collection? FindByFolder(IReadOnlyList<Collection> collections, Guid folderId)
    {
        return collections.FirstOrDefault(collection => FolderTree.FindFolder(collection.Root, folderId) is not null);
    }

    public static Collection? FindByRequest(IReadOnlyList<Collection> collections, Guid requestId)
    {
        return collections.FirstOrDefault(collection => FolderTree.FindRequestParent(collection.Root, requestId) is not null);
    }

    public static RequestDefinition? FindRequest(IReadOnlyList<Collection> collections, Guid requestId)
    {
        return collections
            .Select(collection => FolderTree.FindRequest(collection.Root, requestId))
            .FirstOrDefault(request => request is not null);
    }

    public static List<Collection> Replace(IReadOnlyList<Collection> collections, Guid collectionId, Func<Collection, Collection> change)
    {
        return collections
            .Select(collection => collection.Id == collectionId ? change(collection) : collection)
            .ToList();
    }
}
