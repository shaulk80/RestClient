using RestClient.Domain.Collections;

namespace RestClient.Application.Requests;

// The headers and query parameters that an auth scheme adds to a request.
public sealed record ResolvedAuth(IReadOnlyList<KeyValueEntry> Headers, IReadOnlyList<KeyValueEntry> QueryParams)
{
    public static ResolvedAuth None { get; } = new([], []);
}
