using RestClient.Domain.Collections;

namespace RestClient.Domain.History;

public sealed record HistoryEntry(
    Guid Id,
    DateTimeOffset SentAt,
    RequestMethod Method,
    string ResolvedUrl,
    RequestDefinition RequestSnapshot,
    int? StatusCode,
    double DurationMs,
    string? ErrorMessage,
    string? BodyFilePath);
