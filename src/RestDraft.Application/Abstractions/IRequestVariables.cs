using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

// Builds the variable values a request can use. Secrets are decrypted only when the request refers to them.
public interface IRequestVariables
{
    Task<Result<IReadOnlyDictionary<string, string>>> ResolveAsync(RequestDefinition definition, CancellationToken cancellationToken);
}
