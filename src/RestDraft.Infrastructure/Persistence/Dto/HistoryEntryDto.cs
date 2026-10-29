using RestDraft.Domain.Collections;

namespace RestDraft.Infrastructure.Persistence.Dto;

public sealed record HistoryEntryDto(
    Guid Id,
    long SentAtUnixMs,
    RequestMethod Method,
    string Url,
    RequestDto Request,
    int? StatusCode,
    double DurationMs,
    string? ErrorMessage,
    string? BodyFilePath);
