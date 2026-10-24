using RestClient.Domain.Collections;
using RestClient.Infrastructure.Persistence.Dto;

namespace RestClient.Infrastructure.Persistence.Mappers;

public static class KeyValueMapper
{
    public static IReadOnlyList<KeyValueDto> ToDtos(IReadOnlyList<KeyValueEntry> entries)
    {
        return entries.Select(entry => new KeyValueDto(entry.Key, entry.Value, entry.IsEnabled)).ToList();
    }

    public static IReadOnlyList<KeyValueEntry> ToDomain(IReadOnlyList<KeyValueDto> dtos)
    {
        return dtos.Select(dto => new KeyValueEntry(dto.Key, dto.Value, dto.IsEnabled)).ToList();
    }
}
