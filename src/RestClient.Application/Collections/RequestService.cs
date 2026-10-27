using RestClient.Application.Abstractions;
using RestClient.Application.Workspaces;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Collections;

public sealed class RequestService : IRequestService
{
    private const string NewRequestName = "New request";
    private const string RestoredRequestName = "Restored request";
    private const string CopySuffix = " copy";

    private readonly Workspace<Collection> _workspace;
    private readonly IIdGenerator _idGenerator;

    public RequestService(Workspace<Collection> workspace, IIdGenerator idGenerator)
    {
        _workspace = workspace;
        _idGenerator = idGenerator;
    }

    public async Task<Result<RequestDefinition>> GetAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var loaded = await _workspace.GetAllAsync(cancellationToken);
        if (!loaded.IsSuccess)
        {
            return Result<RequestDefinition>.Failure(loaded.Error!);
        }

        var request = CollectionSet.FindRequest(loaded.Value!, requestId);
        return request is null
            ? Result<RequestDefinition>.Failure(ResultErrors.NotFound("The request"))
            : Result<RequestDefinition>.Success(request);
    }

    public async Task<Result<Guid>> CreateAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var requestId = _idGenerator.NewId();
        var saved = await _workspace.UpdateAsync(collections => Create(collections, folderId, requestId), cancellationToken);
        return saved.IsSuccess ? Result<Guid>.Success(requestId) : Result<Guid>.Failure(saved.Error!);
    }

    public async Task<Result> RenameAsync(Guid requestId, string name, CancellationToken cancellationToken)
    {
        var nameResult = ChildNameRules.ValidateName(name);
        if (!nameResult.IsSuccess)
        {
            return nameResult;
        }

        var trimmed = name.Trim();
        return await _workspace.UpdateAsync(collections => Rename(collections, requestId, trimmed), cancellationToken);
    }

    public Task<Result> UpdateAsync(Guid requestId, Func<RequestDefinition, RequestDefinition> change, CancellationToken cancellationToken)
    {
        return _workspace.UpdateAsync(collections => Update(collections, requestId, change), cancellationToken);
    }

    public async Task<Result<Guid>> DuplicateAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var copyId = _idGenerator.NewId();
        var saved = await _workspace.UpdateAsync(collections => Duplicate(collections, requestId, copyId), cancellationToken);
        return saved.IsSuccess ? Result<Guid>.Success(copyId) : Result<Guid>.Failure(saved.Error!);
    }

    public Task<Result> DeleteAsync(Guid requestId, CancellationToken cancellationToken)
    {
        return _workspace.UpdateAsync(collections => Delete(collections, requestId), cancellationToken);
    }

    public async Task<Result<Guid>> AddCopyAsync(Guid folderId, RequestDefinition source, CancellationToken cancellationToken)
    {
        var copyId = _idGenerator.NewId();
        var saved = await _workspace.UpdateAsync(collections => AddCopy(collections, folderId, source, copyId), cancellationToken);
        return saved.IsSuccess ? Result<Guid>.Success(copyId) : Result<Guid>.Failure(saved.Error!);
    }

    private static Result<IReadOnlyList<Collection>> Create(IReadOnlyList<Collection> collections, Guid folderId, Guid requestId)
    {
        var request = new RequestDefinition
        {
            Id = requestId,
            Name = NewRequestName,
        };
        return Insert(collections, folderId, request);
    }

    private static Result<IReadOnlyList<Collection>> AddCopy(
        IReadOnlyList<Collection> collections,
        Guid folderId,
        RequestDefinition source,
        Guid copyId)
    {
        var name = string.IsNullOrWhiteSpace(source.Name) ? RestoredRequestName : source.Name;
        return Insert(collections, folderId, source with { Id = copyId, Name = name });
    }

    // Inserts the request as the last child of the folder. Its name is made unique among the folder's children.
    private static Result<IReadOnlyList<Collection>> Insert(IReadOnlyList<Collection> collections, Guid folderId, RequestDefinition request)
    {
        var collection = CollectionSet.FindByFolder(collections, folderId);
        if (collection is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The folder"));
        }

        var parent = FolderTree.FindFolder(collection.Root, folderId)!;
        var unique = request with { Name = ChildNameRules.NextAvailableName(request.Name, parent) };
        var root = FolderTree.InsertRequest(collection.Root, folderId, unique, null);
        return Result<IReadOnlyList<Collection>>.Success(ReplaceRoot(collections, collection, root));
    }

    private static Result<IReadOnlyList<Collection>> Rename(IReadOnlyList<Collection> collections, Guid requestId, string name)
    {
        var collection = CollectionSet.FindByRequest(collections, requestId);
        if (collection is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The request"));
        }

        var parent = FolderTree.FindRequestParent(collection.Root, requestId)!;
        var validation = ChildNameRules.ValidateChildName(name, parent, requestId);
        if (!validation.IsSuccess)
        {
            return Result<IReadOnlyList<Collection>>.Failure(validation.Error!);
        }

        var root = FolderTree.UpdateRequest(collection.Root, requestId, request => request with { Name = name });
        return Result<IReadOnlyList<Collection>>.Success(ReplaceRoot(collections, collection, root));
    }

    private static Result<IReadOnlyList<Collection>> Update(
        IReadOnlyList<Collection> collections,
        Guid requestId,
        Func<RequestDefinition, RequestDefinition> change)
    {
        var collection = CollectionSet.FindByRequest(collections, requestId);
        if (collection is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The request"));
        }

        var root = FolderTree.UpdateRequest(collection.Root, requestId, change);
        return Result<IReadOnlyList<Collection>>.Success(ReplaceRoot(collections, collection, root));
    }

    private static Result<IReadOnlyList<Collection>> Duplicate(IReadOnlyList<Collection> collections, Guid requestId, Guid copyId)
    {
        var collection = CollectionSet.FindByRequest(collections, requestId);
        if (collection is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The request"));
        }

        var original = FolderTree.FindRequest(collection.Root, requestId)!;
        var parent = FolderTree.FindRequestParent(collection.Root, requestId)!;
        var copy = original with
        {
            Id = copyId,
            Name = ChildNameRules.NextAvailableName(original.Name + CopySuffix, parent),
        };
        var root = FolderTree.InsertRequestAfter(collection.Root, requestId, copy);
        return Result<IReadOnlyList<Collection>>.Success(ReplaceRoot(collections, collection, root));
    }

    private static Result<IReadOnlyList<Collection>> Delete(IReadOnlyList<Collection> collections, Guid requestId)
    {
        var collection = CollectionSet.FindByRequest(collections, requestId);
        if (collection is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The request"));
        }

        var root = FolderTree.RemoveRequest(collection.Root, requestId);
        return Result<IReadOnlyList<Collection>>.Success(ReplaceRoot(collections, collection, root));
    }

    private static List<Collection> ReplaceRoot(IReadOnlyList<Collection> collections, Collection collection, Folder root)
    {
        return CollectionSet.Replace(collections, collection.Id, current => current with { Root = root });
    }
}
