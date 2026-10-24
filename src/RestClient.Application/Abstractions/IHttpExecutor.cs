using RestClient.Application.Requests;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;

namespace RestClient.Application.Abstractions;

public interface IHttpExecutor
{
    Task<Result<ResponseSnapshot>> SendAsync(OutgoingRequest request, CancellationToken cancellationToken);
}
