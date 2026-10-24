using RestClient.Domain.Collections;

namespace RestClient.Application.Requests;

public sealed record OutgoingRequest(
    RequestMethod Method,
    string Url,
    IReadOnlyList<KeyValueEntry> Headers,
    ReadOnlyMemory<byte>? Content,
    TimeSpan Timeout);
