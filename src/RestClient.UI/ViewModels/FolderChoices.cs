using RestClient.Domain.Collections;

namespace RestClient.UI.ViewModels;

public static class FolderChoices
{
    private const string Separator = " / ";

    public static IReadOnlyList<FolderChoice> From(IReadOnlyList<Collection> collections)
    {
        return collections
            .SelectMany(collection => FromFolder(collection.Name, collection.Root, collection.Id))
            .ToList();
    }

    // The tree node of a collection root is the collection itself, so a root folder expands its collection.
    private static IEnumerable<FolderChoice> FromFolder(string path, Folder folder, Guid expandId)
    {
        return [
            new FolderChoice(folder.Id, expandId, path),
            .. folder.Folders.SelectMany(child => FromFolder($"{path}{Separator}{child.Name}", child, child.Id)),
        ];
    }
}
