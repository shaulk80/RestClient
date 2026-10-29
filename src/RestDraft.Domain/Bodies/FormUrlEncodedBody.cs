using RestDraft.Domain.Collections;

namespace RestDraft.Domain.Bodies;

public sealed record FormUrlEncodedBody(IReadOnlyList<KeyValueEntry> Fields) : BodyDefinition;
