using RestDraft.Domain.Collections;

namespace RestDraft.Domain.Responses;

public sealed record ResponseSnapshot(
    int StatusCode,
    string ReasonPhrase,
    IReadOnlyList<KeyValueEntry> Headers,
    string BodyPreview,
    bool IsPreviewTruncated,
    long BodySizeBytes,
    string? BodyFilePath,
    ResponseTiming Timing);
