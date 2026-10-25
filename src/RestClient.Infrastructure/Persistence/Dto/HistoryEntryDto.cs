using RestClient.Domain.Collections;

namespace RestClient.Infrastructure.Persistence.Dto;

public sealed record HistoryEntryDto(
    Guid Id,
    long SentAtUnixMs,
    RequestMethod Method,
    string ResolvedUrl,
    RequestDto Request,
    int? StatusCode,
    double DurationMs,
    string? ErrorMessage,
    string? BodyFilePath);
