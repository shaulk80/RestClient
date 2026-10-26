using Microsoft.Extensions.Logging;
using RestClient.Application.Abstractions;
using RestClient.Domain.Results;

namespace RestClient.Infrastructure.Persistence;

public sealed class SqliteSettingsStore : ISettingsStore
{
    private const string GetSql = "SELECT value FROM settings WHERE key = $key;";
    private const string SetSql =
        "INSERT INTO settings (key, value) VALUES ($key, $value) " +
        "ON CONFLICT (key) DO UPDATE SET value = excluded.value;";

    private static readonly Action<ILogger, Exception?> ReadFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(22, nameof(ReadFailed)),
        "A setting could not be read");

    private static readonly Action<ILogger, Exception?> WriteFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(23, nameof(WriteFailed)),
        "A setting could not be saved");

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly ILogger<SqliteSettingsStore> _logger;

    public SqliteSettingsStore(SqliteConnectionFactory connectionFactory, ILogger<SqliteSettingsStore> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Result<string?>> GetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = GetSql;
            command.Parameters.AddWithValue("$key", key);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Result<string?>.Success(value as string);
        }
        catch (Exception exception)
        {
            ReadFailed(_logger, exception);
            return Result<string?>.Failure(new Error(ErrorCategory.Storage, "A setting could not be read.", exception.ToString()));
        }
    }

    public async Task<Result> SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = SetSql;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception)
        {
            WriteFailed(_logger, exception);
            return Result.Failure(new Error(ErrorCategory.Storage, "A setting could not be saved.", exception.ToString()));
        }
    }
}
