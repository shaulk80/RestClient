using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RestClient.Application.Abstractions;
using RestClient.Application.Tabs;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence.Mappers;
using RestClient.Infrastructure.Persistence.Storage;

namespace RestClient.Infrastructure.Persistence;

public sealed class SqliteOpenTabsStore : IOpenTabsStore
{
    private const string SelectSql =
        "SELECT request_id, is_linked, is_selected, draft FROM open_tabs ORDER BY position;";
    private const string DeleteSql = "DELETE FROM open_tabs;";
    private const string InsertSql =
        "INSERT INTO open_tabs (position, request_id, is_linked, is_selected, draft) " +
        "VALUES ($position, $requestId, $isLinked, $isSelected, $draft);";

    private static readonly Action<ILogger, Exception?> LoadFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(24, nameof(LoadFailed)),
        "Open tabs could not be loaded");

    private static readonly Action<ILogger, Exception?> SaveFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(25, nameof(SaveFailed)),
        "Open tabs could not be saved");

    private static readonly Action<ILogger, Guid, Exception?> DraftUnreadable = LoggerMessage.Define<Guid>(
        LogLevel.Warning,
        new EventId(26, nameof(DraftUnreadable)),
        "The draft of open tab {RequestId} could not be read and the tab was skipped");

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteOpenTabsStore> _logger;

    public SqliteOpenTabsStore(SqliteConnectionFactory connectionFactory, ILogger<SqliteOpenTabsStore> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<OpenTab>>> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = SelectSql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var tabs = new List<OpenTab>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var tab = ReadTab(reader);
                if (tab is not null)
                {
                    tabs.Add(tab);
                }
            }

            return Result<IReadOnlyList<OpenTab>>.Success(tabs);
        }
        catch (Exception exception)
        {
            LoadFailed(_logger, exception);
            return Result<IReadOnlyList<OpenTab>>.Failure(
                new Error(ErrorCategory.Storage, "The open tabs could not be loaded.", exception.ToString()));
        }
    }

    public async Task<Result> SaveAsync(IReadOnlyList<OpenTab> tabs, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await ClearAsync(connection, transaction, cancellationToken);
            for (var position = 0; position < tabs.Count; position++)
            {
                await InsertAsync(connection, transaction, position, tabs[position], cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception)
        {
            SaveFailed(_logger, exception);
            return Result.Failure(new Error(ErrorCategory.Storage, "The open tabs could not be saved.", exception.ToString()));
        }
    }

    private static async Task ClearAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = DeleteSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int position,
        OpenTab tab,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = InsertSql;
        command.Parameters.AddWithValue("$position", position);
        command.Parameters.AddWithValue("$requestId", tab.RequestId.ToString());
        command.Parameters.AddWithValue("$isLinked", tab.IsLinked ? 1 : 0);
        command.Parameters.AddWithValue("$isSelected", tab.IsSelected ? 1 : 0);
        command.Parameters.AddWithValue("$draft", tab.Draft is null ? DBNull.Value : SerializeDraft(tab.Draft));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SerializeDraft(RequestDefinition draft)
    {
        return RequestJson.Serialize(RequestMapper.ToDto(draft));
    }

    // A tab is skipped when its draft cannot be read. The tab is still kept if there is no draft to read.
    private OpenTab? ReadTab(SqliteDataReader reader)
    {
        var requestId = Guid.Parse(reader.GetString(0));
        var isLinked = reader.GetInt32(1) != 0;
        var isSelected = reader.GetInt32(2) != 0;
        if (reader.IsDBNull(3))
        {
            return new OpenTab(requestId, isLinked, isSelected, null);
        }

        try
        {
            var dto = RequestJson.Deserialize(reader.GetString(3));
            var draft = dto is null ? null : RequestMapper.ToDomain(dto).Value;
            if (draft is null)
            {
                DraftUnreadable(_logger, requestId, null);
                return null;
            }

            return new OpenTab(requestId, isLinked, isSelected, draft);
        }
        catch (JsonException exception)
        {
            DraftUnreadable(_logger, requestId, exception);
            return null;
        }
    }
}
