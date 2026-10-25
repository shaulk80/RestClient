using RestClient.Application.History;
using RestClient.Application.Requests;
using RestClient.Domain.Collections;
using RestClient.Domain.History;
using RestClient.Domain.Results;

namespace RestClient.Application.Abstractions;

public interface IHistoryService
{
    Task<Result> RecordAsync(RequestAttempt attempt, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<HistoryEntry>>> SearchAsync(HistoryFilter filter, CancellationToken cancellationToken);

    Task<Result<RequestDefinition>> RestoreAsync(Guid entryId, CancellationToken cancellationToken);
}
