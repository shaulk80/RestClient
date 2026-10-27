using RestClient.Application.Abstractions;
using RestClient.Application.Workspaces;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Collections;

public sealed class FolderService : IFolderService
{
    private readonly Workspace<Collection> _workspace;
    private readonly IIdGenerator _idGenerator;

    public FolderService(Workspace<Collection> workspace, IIdGenerator idGenerator)
    {
        _workspace = workspace;
        _idGenerator = idGenerator;
    }

    public async Task<Result<Guid>> CreateAsync(Guid parentFolderId, CancellationToken cancellationToken)
    {
        var folderId = _idGenerator.NewId();
        var saved = await _workspace.UpdateAsync(collections => Create(collections, parentFolderId, folderId), cancellationToken);
        return saved.IsSuccess ? Result<Guid>.Success(folderId) : Result<Guid>.Failure(saved.Error!);
    }

    public async Task<Result> RenameAsync(Guid folderId, string name, CancellationToken cancellationToken)
    {
        var nameResult = ChildNameRules.ValidateName(name);
        if (!nameResult.IsSuccess)
        {
            return nameResult;
        }

        var trimmed = name.Trim();
        return await _workspace.UpdateAsync(collections => Rename(collections, folderId, trimmed), cancellationToken);
    }

    public Task<Result> DeleteAsync(Guid folderId, CancellationToken cancellationToken)
    {
        return _workspace.UpdateAsync(collections => Delete(collections, folderId), cancellationToken);
    }

    private static Result<IReadOnlyList<Collection>> Create(IReadOnlyList<Collection> collections, Guid parentFolderId, Guid folderId)
    {
        var collection = CollectionSet.FindByFolder(collections, parentFolderId);
        if (collection is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The folder"));
        }

        var parent = FolderTree.FindFolder(collection.Root, parentFolderId)!;
        var folder = new Folder(folderId, ChildNameRules.NewFolderName(parent), [], []);
        var root = FolderTree.InsertFolder(collection.Root, parentFolderId, folder, null);
        return Result<IReadOnlyList<Collection>>.Success(ReplaceRoot(collections, collection, root));
    }

    private static Result<IReadOnlyList<Collection>> Rename(IReadOnlyList<Collection> collections, Guid folderId, string name)
    {
        var collection = CollectionSet.FindByFolder(collections, folderId);
        if (collection is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The folder"));
        }

        var parent = FolderTree.FindFolderParent(collection.Root, folderId);
        if (parent is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(
                ResultErrors.Validation("The collection root cannot be renamed. Rename the collection instead."));
        }

        var validation = ChildNameRules.ValidateChildName(name, parent, folderId);
        if (!validation.IsSuccess)
        {
            return Result<IReadOnlyList<Collection>>.Failure(validation.Error!);
        }

        var root = FolderTree.ReplaceFolder(collection.Root, folderId, folder => folder with { Name = name });
        return Result<IReadOnlyList<Collection>>.Success(ReplaceRoot(collections, collection, root));
    }

    private static Result<IReadOnlyList<Collection>> Delete(IReadOnlyList<Collection> collections, Guid folderId)
    {
        var collection = CollectionSet.FindByFolder(collections, folderId);
        if (collection is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The folder"));
        }

        if (collection.Root.Id == folderId)
        {
            return Result<IReadOnlyList<Collection>>.Failure(
                ResultErrors.Validation("The collection root cannot be deleted. Delete the collection instead."));
        }

        var root = FolderTree.RemoveFolder(collection.Root, folderId);
        return Result<IReadOnlyList<Collection>>.Success(ReplaceRoot(collections, collection, root));
    }

    private static List<Collection> ReplaceRoot(IReadOnlyList<Collection> collections, Collection collection, Folder root)
    {
        return CollectionSet.Replace(collections, collection.Id, current => current with { Root = root });
    }
}
