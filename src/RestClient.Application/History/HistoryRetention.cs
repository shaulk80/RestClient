using RestClient.Application.Abstractions;
using RestClient.Domain.History;
using RestClient.Domain.Results;

namespace RestClient.Application.History;

// Applies a RetentionPolicy: removes expired entries, then their spilled response bodies.
public sealed class HistoryRetention
{
    private readonly IHistoryRepository _repository;
    private readonly IBodyFileStore _bodyFiles;
    private readonly RetentionPolicy _policy;

    public HistoryRetention(IHistoryRepository repository, IBodyFileStore bodyFiles, RetentionPolicy policy)
    {
        _repository = repository;
        _bodyFiles = bodyFiles;
        _policy = policy;
    }

    public async Task<Result> ApplyAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entries = await _repository.SearchAsync(HistoryFilter.All, cancellationToken);
        if (!entries.IsSuccess)
        {
            return Result.Failure(entries.Error!);
        }

        var expired = _policy.SelectExpired(entries.Value!, now);
        if (expired.Count == 0)
        {
            return Result.Success();
        }

        var deleted = await _repository.DeleteAsync(expired.Select(entry => entry.Id).ToList(), cancellationToken);
        if (!deleted.IsSuccess)
        {
            return deleted;
        }

        return DeleteBodyFiles(expired);
    }

    // The entries are already gone, so every file is attempted. The first failure is returned.
    private Result DeleteBodyFiles(IReadOnlyList<HistoryEntry> expired)
    {
        var failures = expired
            .Select(entry => entry.BodyFilePath)
            .OfType<string>()
            .Select(_bodyFiles.Delete)
            .Where(result => !result.IsSuccess)
            .ToList();
        return failures.FirstOrDefault() ?? Result.Success();
    }
}
