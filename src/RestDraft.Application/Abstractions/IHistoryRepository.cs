using RestDraft.Application.History;
using RestDraft.Domain.History;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

public interface IHistoryRepository
{
    Task<Result> AddAsync(HistoryEntry entry, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<HistoryEntry>>> SearchAsync(HistoryFilter filter, CancellationToken cancellationToken);

    Task<Result<HistoryEntry>> GetAsync(Guid entryId, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(IReadOnlyList<Guid> entryIds, CancellationToken cancellationToken);
}
