using RestDraft.Domain.Auth;
using RestDraft.Domain.Bodies;

namespace RestDraft.Domain.Collections;

public sealed record RequestDefinition
{
    public required Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public RequestMethod Method { get; init; } = RequestMethod.Get;

    public string Url { get; init; } = string.Empty;

    public IReadOnlyList<KeyValueEntry> QueryParams { get; init; } = [];

    public IReadOnlyList<KeyValueEntry> Headers { get; init; } = [];

    public BodyDefinition Body { get; init; } = new NoBody();

    public AuthDefinition Auth { get; init; } = new NoAuth();
}
