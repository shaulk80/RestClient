using RestDraft.Domain.Results;

namespace RestDraft.Application.Workspaces;

// Holds a loaded list and serializes every read and write. Each update saves the whole list in one call.
public sealed class Workspace<T> : IDisposable
{
    private readonly Func<CancellationToken, Task<Result<IReadOnlyList<T>>>> _load;
    private readonly Func<IReadOnlyList<T>, CancellationToken, Task<Result>> _save;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<T>? _items;

    public Workspace(
        Func<CancellationToken, Task<Result<IReadOnlyList<T>>>> load,
        Func<IReadOnlyList<T>, CancellationToken, Task<Result>> save)
    {
        _load = load;
        _save = save;
    }

    public async Task<Result<IReadOnlyList<T>>> GetAllAsync(CancellationToken cancellationToken)
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
        Func<IReadOnlyList<T>, Result<IReadOnlyList<T>>> change,
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

            var saved = await _save(changed.Value!, cancellationToken);
            if (saved.IsSuccess)
            {
                _items = changed.Value;
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

    private async Task<Result<IReadOnlyList<T>>> LoadIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_items is not null)
        {
            return Result<IReadOnlyList<T>>.Success(_items);
        }

        var loaded = await _load(cancellationToken);
        if (loaded.IsSuccess)
        {
            _items = loaded.Value;
        }

        return loaded;
    }
}
