using Microsoft.Data.Sqlite;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence.Dto;

namespace RestClient.Infrastructure.Persistence.Storage;

public static class HistoryEntryDtoReader
{
    public static async Task<Result<IReadOnlyList<HistoryEntryDto>>> ReadAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var dtos = new List<HistoryEntryDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var dto = ReadRow(reader);
            if (dto is null)
            {
                return Result<IReadOnlyList<HistoryEntryDto>>.Failure(new Error(ErrorCategory.Storage, "A stored history entry is damaged."));
            }

            dtos.Add(dto);
        }

        return Result<IReadOnlyList<HistoryEntryDto>>.Success(dtos);
    }

    // Columns: id, sent_at, method, url, request_json, status_code, duration_ms, error_message, body_file_path.
    private static HistoryEntryDto? ReadRow(SqliteDataReader reader)
    {
        var request = RequestJson.Deserialize(reader.GetString(4));
        if (request is null || !Enum.TryParse<RequestMethod>(reader.GetString(2), out var method))
        {
            return null;
        }

        return new HistoryEntryDto(
            reader.GetGuid(0),
            reader.GetInt64(1),
            method,
            reader.GetString(3),
            request,
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.GetDouble(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8));
    }
}
