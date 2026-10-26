using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;

namespace RestClient.Application.Abstractions;

public interface IRequestSender
{
    Task<Result<ResponseSnapshot>> SendAsync(RequestDefinition definition, CancellationToken cancellationToken);
}
