using RestDraft.Application.Abstractions;
using RestDraft.Application.Workspaces;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Collections;

public sealed class CollectionService : ICollectionService
{
    private readonly Workspace<Collection> _workspace;
    private readonly IIdGenerator _idGenerator;

    public CollectionService(Workspace<Collection> workspace, IIdGenerator idGenerator)
    {
        _workspace = workspace;
        _idGenerator = idGenerator;
    }

    public Task<Result<IReadOnlyList<Collection>>> ListAsync(CancellationToken cancellationToken)
    {
        return _workspace.GetAllAsync(cancellationToken);
    }

    public async Task<Result<Guid>> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var nameResult = ChildNameRules.ValidateName(name);
        if (!nameResult.IsSuccess)
        {
            return Result<Guid>.Failure(nameResult.Error!);
        }

        var collection = new Collection(_idGenerator.NewId(), name.Trim(), new Folder(_idGenerator.NewId(), string.Empty, [], []));
        var saved = await _workspace.UpdateAsync(
            collections => Result<IReadOnlyList<Collection>>.Success([.. collections, collection]),
            cancellationToken);
        return saved.IsSuccess ? Result<Guid>.Success(collection.Id) : Result<Guid>.Failure(saved.Error!);
    }

    public async Task<Result> RenameAsync(Guid collectionId, string name, CancellationToken cancellationToken)
    {
        var nameResult = ChildNameRules.ValidateName(name);
        if (!nameResult.IsSuccess)
        {
            return nameResult;
        }

        var trimmed = name.Trim();
        return await _workspace.UpdateAsync(collections => Rename(collections, collectionId, trimmed), cancellationToken);
    }

    public Task<Result> DeleteAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        return _workspace.UpdateAsync(collections => Delete(collections, collectionId), cancellationToken);
    }

    private static Result<IReadOnlyList<Collection>> Rename(IReadOnlyList<Collection> collections, Guid collectionId, string name)
    {
        if (CollectionSet.Find(collections, collectionId) is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The collection"));
        }

        return Result<IReadOnlyList<Collection>>.Success(
            CollectionSet.Replace(collections, collectionId, collection => collection with { Name = name }));
    }

    private static Result<IReadOnlyList<Collection>> Delete(IReadOnlyList<Collection> collections, Guid collectionId)
    {
        if (CollectionSet.Find(collections, collectionId) is null)
        {
            return Result<IReadOnlyList<Collection>>.Failure(ResultErrors.NotFound("The collection"));
        }

        return Result<IReadOnlyList<Collection>>.Success(collections.Where(collection => collection.Id != collectionId).ToList());
    }
}
