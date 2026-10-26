using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Abstractions;

// Builds the variable values a request can use. Secrets are decrypted only when the request refers to them.
public interface IRequestVariables
{
    Task<Result<IReadOnlyDictionary<string, string>>> ResolveAsync(RequestDefinition definition, CancellationToken cancellationToken);
}
