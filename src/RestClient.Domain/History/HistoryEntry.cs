using RestClient.Domain.Collections;

namespace RestClient.Domain.History;

public sealed record HistoryEntry(
    Guid Id,
    DateTimeOffset SentAt,
    RequestMethod Method,
    string Url,
    RequestDefinition RequestSnapshot,
    int? StatusCode,
    double DurationMs,
    string? ErrorMessage,
    string? BodyFilePath);
