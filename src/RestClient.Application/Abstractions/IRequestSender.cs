using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;

namespace RestClient.Application.Abstractions;

public interface IRequestSender
{
    Task<Result<ResponseSnapshot>> SendAsync(
        RequestDefinition definition,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken);
}
