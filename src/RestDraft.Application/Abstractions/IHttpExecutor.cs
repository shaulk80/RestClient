using RestDraft.Application.Requests;
using RestDraft.Domain.Responses;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

public interface IHttpExecutor
{
    Task<Result<ResponseSnapshot>> SendAsync(OutgoingRequest request, CancellationToken cancellationToken);
}
