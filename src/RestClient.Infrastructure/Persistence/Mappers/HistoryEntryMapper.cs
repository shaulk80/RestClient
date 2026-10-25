using RestClient.Domain.History;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence.Dto;

namespace RestClient.Infrastructure.Persistence.Mappers;

public static class HistoryEntryMapper
{
    public static HistoryEntryDto ToDto(HistoryEntry entry)
    {
        return new HistoryEntryDto(
            entry.Id,
            entry.SentAt.ToUnixTimeMilliseconds(),
            entry.Method,
            entry.ResolvedUrl,
            RequestMapper.ToDto(entry.RequestSnapshot),
            entry.StatusCode,
            entry.DurationMs,
            entry.ErrorMessage,
            entry.BodyFilePath);
    }

    public static Result<IReadOnlyList<HistoryEntry>> ToDomain(IReadOnlyList<HistoryEntryDto> dtos)
    {
        var entries = new List<HistoryEntry>();
        foreach (var dto in dtos)
        {
            var entry = ToDomain(dto);
            if (!entry.IsSuccess)
            {
                return Result<IReadOnlyList<HistoryEntry>>.Failure(entry.Error!);
            }

            entries.Add(entry.Value!);
        }

        return Result<IReadOnlyList<HistoryEntry>>.Success(entries);
    }

    public static Result<HistoryEntry> ToDomain(HistoryEntryDto dto)
    {
        var request = RequestMapper.ToDomain(dto.Request);
        if (!request.IsSuccess)
        {
            return Result<HistoryEntry>.Failure(request.Error!);
        }

        return Result<HistoryEntry>.Success(new HistoryEntry(
            dto.Id,
            DateTimeOffset.FromUnixTimeMilliseconds(dto.SentAtUnixMs),
            dto.Method,
            dto.ResolvedUrl,
            request.Value!,
            dto.StatusCode,
            dto.DurationMs,
            dto.ErrorMessage,
            dto.BodyFilePath));
    }
}
