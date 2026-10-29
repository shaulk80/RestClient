using RestDraft.Domain.Collections;
using RestDraft.Domain.Responses;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

public interface IRequestSender
{
    Task<Result<ResponseSnapshot>> SendAsync(RequestDefinition definition, CancellationToken cancellationToken);
}
