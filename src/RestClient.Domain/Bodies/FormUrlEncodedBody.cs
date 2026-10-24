using RestClient.Domain.Collections;

namespace RestClient.Domain.Bodies;

public sealed record FormUrlEncodedBody(IReadOnlyList<KeyValueEntry> Fields) : BodyDefinition;
