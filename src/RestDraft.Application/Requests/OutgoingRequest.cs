using RestDraft.Domain.Collections;

namespace RestDraft.Application.Requests;

public sealed record OutgoingRequest(
    RequestMethod Method,
    string Url,
    IReadOnlyList<KeyValueEntry> Headers,
    ReadOnlyMemory<byte>? Content,
    TimeSpan Timeout);
