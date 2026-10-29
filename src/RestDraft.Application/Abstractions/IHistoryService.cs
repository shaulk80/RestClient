using RestDraft.Application.History;
using RestDraft.Application.Requests;
using RestDraft.Domain.Collections;
using RestDraft.Domain.History;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

public interface IHistoryService
{
    Task<Result> RecordAsync(RequestAttempt attempt, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<HistoryEntry>>> SearchAsync(HistoryFilter filter, CancellationToken cancellationToken);

    Task<Result<RequestDefinition>> RestoreAsync(Guid entryId, CancellationToken cancellationToken);
}
