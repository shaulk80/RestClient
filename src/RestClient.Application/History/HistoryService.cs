using RestClient.Application.Abstractions;
using RestClient.Application.Requests;
using RestClient.Domain.Collections;
using RestClient.Domain.History;
using RestClient.Domain.Results;

namespace RestClient.Application.History;

// Records every send and serializes the writes, so two sends finishing together cannot interleave
// their inserts and retention passes.
public sealed class HistoryService : IHistoryService, IDisposable
{
    private readonly IHistoryRepository _repository;
    private readonly HistoryRetention _retention;
    private readonly IIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public HistoryService(
        IHistoryRepository repository,
        HistoryRetention retention,
        IIdGenerator idGenerator,
        IClock clock)
    {
        _repository = repository;
        _retention = retention;
        _idGenerator = idGenerator;
        _clock = clock;
    }

    public async Task<Result> RecordAsync(RequestAttempt attempt, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var added = await _repository.AddAsync(ToEntry(attempt), cancellationToken);
            if (!added.IsSuccess)
            {
                return added;
            }

            return await _retention.ApplyAsync(_clock.UtcNow, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<Result<IReadOnlyList<HistoryEntry>>> SearchAsync(HistoryFilter filter, CancellationToken cancellationToken)
    {
        return _repository.SearchAsync(filter, cancellationToken);
    }

    // Returns an unsaved definition. It gets a new identifier, so it cannot be mistaken for the saved request it came from.
    // Nothing is written to a collection here. The caller decides where the request is saved.
    public async Task<Result<RequestDefinition>> RestoreAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetAsync(entryId, cancellationToken);
        if (!entry.IsSuccess)
        {
            return Result<RequestDefinition>.Failure(entry.Error!);
        }

        var restored = entry.Value!.RequestSnapshot with { Id = _idGenerator.NewId() };
        return Result<RequestDefinition>.Success(restored);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private HistoryEntry ToEntry(RequestAttempt attempt)
    {
        var outcome = attempt.Outcome;
        return new HistoryEntry(
            _idGenerator.NewId(),
            attempt.SentAt,
            attempt.Request.Method,
            attempt.Url,
            attempt.Request,
            outcome.Value?.StatusCode,
            attempt.DurationMs,
            ErrorText(outcome.Error),
            outcome.Value?.BodyFilePath);
    }

    private static string? ErrorText(Error? error)
    {
        if (error is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(error.Details)
            ? error.Message
            : $"{error.Message} ({error.Details})";
    }
}
