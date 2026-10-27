using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RestClient.Application.Abstractions;
using RestClient.Application.History;
using RestClient.Domain.History;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence.Dto;
using RestClient.Infrastructure.Persistence.Mappers;
using RestClient.Infrastructure.Persistence.Storage;

namespace RestClient.Infrastructure.Persistence;

public sealed class SqliteHistoryRepository : IHistoryRepository
{
    private const string SelectColumnsSql =
        "SELECT id, sent_at, method, url, request_json, status_code, duration_ms, error_message, body_file_path FROM history_entries";
    private const string SearchSql =
        SelectColumnsSql +
        " WHERE ($url = '' OR instr(lower(url), lower($url)) > 0)" +
        " AND ($method IS NULL OR method = $method)" +
        " ORDER BY sent_at DESC, rowid DESC;";
    private const string GetSql = SelectColumnsSql + " WHERE id = $id;";
    private const string InsertSql =
        "INSERT INTO history_entries (id, sent_at, method, url, request_json, status_code, duration_ms, error_message, body_file_path) " +
        "VALUES ($id, $sentAt, $method, $url, $request, $statusCode, $durationMs, $errorMessage, $bodyFilePath);";
    private const string DeleteSql = "DELETE FROM history_entries WHERE id = $id;";

    private static readonly Action<ILogger, Exception?> AddFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(10, nameof(AddFailed)),
        "History entry could not be saved");

    private static readonly Action<ILogger, Exception?> ReadFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(11, nameof(ReadFailed)),
        "History could not be read");

    private static readonly Action<ILogger, Exception?> DeleteFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(12, nameof(DeleteFailed)),
        "History entries could not be deleted");

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteHistoryRepository> _logger;

    public SqliteHistoryRepository(SqliteConnectionFactory connectionFactory, ILogger<SqliteHistoryRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result> AddAsync(HistoryEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = InsertSql;
            AddParameters(command, HistoryEntryMapper.ToDto(entry));
            await command.ExecuteNonQueryAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception)
        {
            AddFailed(_logger, exception);
            return Result.Failure(new Error(ErrorCategory.Storage, "The history entry could not be saved.", exception.ToString()));
        }
    }

    public async Task<Result<IReadOnlyList<HistoryEntry>>> SearchAsync(HistoryFilter filter, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = SearchSql;
            command.Parameters.AddWithValue("$url", filter.UrlText);
            command.Parameters.AddWithValue("$method", (object?)filter.Method?.ToString() ?? DBNull.Value);
            return await ReadEntriesAsync(command, cancellationToken);
        }
        catch (Exception exception)
        {
            ReadFailed(_logger, exception);
            return Result<IReadOnlyList<HistoryEntry>>.Failure(new Error(ErrorCategory.Storage, "The history could not be read.", exception.ToString()));
        }
    }

    public async Task<Result<HistoryEntry>> GetAsync(Guid entryId, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = GetSql;
            command.Parameters.AddWithValue("$id", entryId);
            var entries = await ReadEntriesAsync(command, cancellationToken);
            if (!entries.IsSuccess)
            {
                return Result<HistoryEntry>.Failure(entries.Error!);
            }

            var found = entries.Value!;
            return found.Count == 0
                ? Result<HistoryEntry>.Failure(ResultErrors.NotFound("The history entry"))
                : Result<HistoryEntry>.Success(found[0]);
        }
        catch (Exception exception)
        {
            ReadFailed(_logger, exception);
            return Result<HistoryEntry>.Failure(new Error(ErrorCategory.Storage, "The history could not be read.", exception.ToString()));
        }
    }

    public async Task<Result> DeleteAsync(IReadOnlyList<Guid> entryIds, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            foreach (var entryId in entryIds)
            {
                await DeleteOneAsync(connection, entryId, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception)
        {
            DeleteFailed(_logger, exception);
            return Result.Failure(new Error(ErrorCategory.Storage, "The history entries could not be deleted.", exception.ToString()));
        }
    }

    private static async Task<Result<IReadOnlyList<HistoryEntry>>> ReadEntriesAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var dtos = await HistoryEntryDtoReader.ReadAsync(command, cancellationToken);
        return dtos.IsSuccess
            ? HistoryEntryMapper.ToDomain(dtos.Value!)
            : Result<IReadOnlyList<HistoryEntry>>.Failure(dtos.Error!);
    }

    private static void AddParameters(SqliteCommand command, HistoryEntryDto dto)
    {
        command.Parameters.AddWithValue("$id", dto.Id);
        command.Parameters.AddWithValue("$sentAt", dto.SentAtUnixMs);
        command.Parameters.AddWithValue("$method", dto.Method.ToString());
        command.Parameters.AddWithValue("$url", dto.Url);
        command.Parameters.AddWithValue("$request", RequestJson.Serialize(dto.Request));
        command.Parameters.AddWithValue("$statusCode", (object?)dto.StatusCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$durationMs", dto.DurationMs);
        command.Parameters.AddWithValue("$errorMessage", (object?)dto.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$bodyFilePath", (object?)dto.BodyFilePath ?? DBNull.Value);
    }

    private static async Task DeleteOneAsync(SqliteConnection connection, Guid entryId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = DeleteSql;
        command.Parameters.AddWithValue("$id", entryId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
