using RestDraft.Domain.Results;

namespace RestDraft.Application.Settings;

// Keeps the current settings in memory after the first read. A save replaces them for the next send.
public sealed class SettingsService : ISettingsService, IDisposable
{
    private readonly AppSettingsStorage _storage;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AppSettings? _current;

    public SettingsService(AppSettingsStorage storage)
    {
        _storage = storage;
    }

    public async Task<Result<AppSettings>> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_current is not null)
            {
                return Result<AppSettings>.Success(_current);
            }

            var loaded = await _storage.LoadAsync(cancellationToken);
            if (loaded.IsSuccess)
            {
                _current = loaded.Value;
            }

            return loaded;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result> SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var validation = AppSettingsValidator.Validate(settings);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var saved = await _storage.SaveAsync(settings, cancellationToken);
            if (saved.IsSuccess)
            {
                _current = settings;
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
}
