using RestClient.Application.Abstractions;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Collections;

// Holds the loaded collections and serializes every read and write, so concurrent tree edits
// cannot overwrite each other. Each update saves the whole workspace in one transaction.
public sealed class CollectionWorkspace : IDisposable
{
    private readonly ICollectionRepository _repository;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<Collection>? _collections;

    public CollectionWorkspace(ICollectionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<Collection>>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadIfNeededAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result> UpdateAsync(
        Func<IReadOnlyList<Collection>, Result<IReadOnlyList<Collection>>> change,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var loaded = await LoadIfNeededAsync(cancellationToken);
            if (!loaded.IsSuccess)
            {
                return Result.Failure(loaded.Error!);
            }

            var changed = change(loaded.Value!);
            if (!changed.IsSuccess)
            {
                return Result.Failure(changed.Error!);
            }

            var saved = await _repository.SaveAsync(changed.Value!, cancellationToken);
            if (saved.IsSuccess)
            {
                _collections = changed.Value;
            }

            return saved;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task<Result<IReadOnlyList<Collection>>> LoadIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_collections is not null)
        {
            return Result<IReadOnlyList<Collection>>.Success(_collections);
        }

        var loaded = await _repository.LoadAsync(cancellationToken);
        if (loaded.IsSuccess)
        {
            _collections = loaded.Value;
        }

        return loaded;
    }
}
