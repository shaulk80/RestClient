using RestClient.Application.Workspaces;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Collections;

public sealed class CollectionTreeService : ICollectionTreeService
{
    private readonly Workspace<Collection> _workspace;

    public CollectionTreeService(Workspace<Collection> workspace)
    {
        _workspace = workspace;
    }

    public Task<Result> MoveAsync(Guid nodeId, Guid targetFolderId, Guid? beforeNodeId, CancellationToken cancellationToken)
    {
        return _workspace.UpdateAsync(
            collections => Move(collections, nodeId, targetFolderId, beforeNodeId),
            cancellationToken);
    }

    private static Result<IReadOnlyList<Collection>> Move(
        IReadOnlyList<Collection> collections,
        Guid nodeId,
        Guid targetFolderId,
        Guid? beforeNodeId)
    {
        if (CollectionSet.FindByFolder(collections, targetFolderId) is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The target folder"));
        }

        if (CollectionSet.FindByFolder(collections, nodeId) is not null)
        {
            return MoveFolder(collections, nodeId, targetFolderId, beforeNodeId);
        }

        if (CollectionSet.FindByRequest(collections, nodeId) is not null)
        {
            return MoveRequest(collections, nodeId, targetFolderId, beforeNodeId);
        }

        return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The item to move"));
    }

    private static Result<IReadOnlyList<Collection>> MoveFolder(
        IReadOnlyList<Collection> collections,
        Guid folderId,
        Guid targetFolderId,
        Guid? beforeNodeId)
    {
        var source = CollectionSet.FindByFolder(collections, folderId)!;
        var folder = FolderTree.FindFolder(source.Root, folderId)!;
        if (folder.Id == source.Root.Id)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.Validation("The collection root cannot be moved."));
        }

        if (FolderTree.FindFolder(folder, targetFolderId) is not null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.Validation("A folder cannot be moved into itself."));
        }

        var detached = CollectionSet.Replace(collections, source.Id, current => current with
        {
            Root = FolderTree.RemoveFolder(current.Root, folderId),
        });
        var target = CollectionSet.FindByFolder(detached, targetFolderId)!;
        var targetFolder = FolderTree.FindFolder(target.Root, targetFolderId)!;
        var validation = ChildNameRules.ValidateChildName(folder.Name, targetFolder, folderId);
        if (!validation.IsSuccess)
        {
            return Result<IReadOnlyList<Collection>>.Failure(validation.Error!);
        }

        return Result<IReadOnlyList<Collection>>.Success(CollectionSet.Replace(detached, target.Id, current => current with
        {
            Root = FolderTree.InsertFolder(current.Root, targetFolderId, folder, beforeNodeId),
        }));
    }

    private static Result<IReadOnlyList<Collection>> MoveRequest(
        IReadOnlyList<Collection> collections,
        Guid requestId,
        Guid targetFolderId,
        Guid? beforeNodeId)
    {
        var source = CollectionSet.FindByRequest(collections, requestId)!;
        var request = FolderTree.FindRequest(source.Root, requestId)!;
        var detached = CollectionSet.Replace(collections, source.Id, current => current with
        {
            Root = FolderTree.RemoveRequest(current.Root, requestId),
        });
        var target = CollectionSet.FindByFolder(detached, targetFolderId)!;
        var targetFolder = FolderTree.FindFolder(target.Root, targetFolderId)!;
        var validation = ChildNameRules.ValidateChildName(request.Name, targetFolder, requestId);
        if (!validation.IsSuccess)
        {
            return Result<IReadOnlyList<Collection>>.Failure(validation.Error!);
        }

        return Result<IReadOnlyList<Collection>>.Success(CollectionSet.Replace(detached, target.Id, current => current with
        {
            Root = FolderTree.InsertRequest(current.Root, targetFolderId, request, beforeNodeId),
        }));
    }
}
