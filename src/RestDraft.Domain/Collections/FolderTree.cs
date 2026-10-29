namespace RestDraft.Domain.Collections;

public static class FolderTree
{
    public static Folder? FindFolder(Folder folder, Guid folderId)
    {
        if (folder.Id == folderId)
        {
            return folder;
        }

        return folder.Folders
            .Select(child => FindFolder(child, folderId))
            .FirstOrDefault(found => found is not null);
    }

    public static Folder? FindFolderParent(Folder folder, Guid folderId)
    {
        return FindParent(folder, candidate => candidate.Folders.Any(child => child.Id == folderId));
    }

    public static Folder? FindRequestParent(Folder folder, Guid requestId)
    {
        return FindParent(folder, candidate => candidate.Requests.Any(request => request.Id == requestId));
    }

    public static RequestDefinition? FindRequest(Folder folder, Guid requestId)
    {
        return FindRequestParent(folder, requestId)?.Requests.FirstOrDefault(request => request.Id == requestId);
    }

    public static Folder ReplaceFolder(Folder folder, Guid folderId, Func<Folder, Folder> change)
    {
        if (folder.Id == folderId)
        {
            return change(folder);
        }

        return folder with
        {
            Folders = folder.Folders.Select(child => ReplaceFolder(child, folderId, change)).ToList(),
        };
    }

    public static Folder RemoveFolder(Folder root, Guid folderId)
    {
        var parent = FindFolderParent(root, folderId);
        if (parent is null)
        {
            return root;
        }

        return ReplaceFolder(root, parent.Id, folder => folder with
        {
            Folders = folder.Folders.Where(child => child.Id != folderId).ToList(),
        });
    }

    public static Folder RemoveRequest(Folder root, Guid requestId)
    {
        var parent = FindRequestParent(root, requestId);
        if (parent is null)
        {
            return root;
        }

        return ReplaceFolder(root, parent.Id, folder => folder with
        {
            Requests = folder.Requests.Where(request => request.Id != requestId).ToList(),
        });
    }

    public static Folder UpdateRequest(Folder root, Guid requestId, Func<RequestDefinition, RequestDefinition> change)
    {
        var parent = FindRequestParent(root, requestId);
        if (parent is null)
        {
            return root;
        }

        return ReplaceFolder(root, parent.Id, folder => folder with
        {
            Requests = folder.Requests.Select(request => request.Id == requestId ? change(request) : request).ToList(),
        });
    }

    public static Folder InsertFolder(Folder root, Guid parentId, Folder child, Guid? beforeId)
    {
        return ReplaceFolder(root, parentId, parent => parent with
        {
            Folders = InsertBefore(parent.Folders, child, beforeId, folder => folder.Id),
        });
    }

    public static Folder InsertRequest(Folder root, Guid parentId, RequestDefinition request, Guid? beforeId)
    {
        return ReplaceFolder(root, parentId, parent => parent with
        {
            Requests = InsertBefore(parent.Requests, request, beforeId, existing => existing.Id),
        });
    }

    public static Folder InsertRequestAfter(Folder root, Guid siblingId, RequestDefinition request)
    {
        var parent = FindRequestParent(root, siblingId);
        if (parent is null)
        {
            return root;
        }

        var nextId = parent.Requests
            .SkipWhile(existing => existing.Id != siblingId)
            .Skip(1)
            .FirstOrDefault()?.Id;
        return InsertRequest(root, parent.Id, request, nextId);
    }

    private static Folder? FindParent(Folder folder, Func<Folder, bool> hasChild)
    {
        if (hasChild(folder))
        {
            return folder;
        }

        return folder.Folders
            .Select(child => FindParent(child, hasChild))
            .FirstOrDefault(found => found is not null);
    }

    private static List<T> InsertBefore<T>(IReadOnlyList<T> items, T item, Guid? beforeId, Func<T, Guid> idOf)
    {
        var result = items.ToList();
        var index = beforeId is null
            ? -1
            : result.FindIndex(existing => idOf(existing) == beforeId);
        if (index < 0)
        {
            result.Add(item);
        }
        else
        {
            result.Insert(index, item);
        }

        return result;
    }
}
