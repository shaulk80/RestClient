using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace RestDraft.Infrastructure.Persistence;

public sealed class SqlScriptMigration : IMigration
{
    private static readonly Action<ILogger, string, Exception?> ScriptFailed = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(2, nameof(ScriptFailed)),
        "SQL script {ScriptPath} failed");

    private readonly IReadOnlyList<string> _scriptPaths;
    private readonly ILogger _logger;

    public SqlScriptMigration(int version, IReadOnlyList<string> scriptPaths, ILogger logger)
    {
        Version = version;
        _scriptPaths = scriptPaths;
        _logger = logger;
    }

    public int Version { get; }

    public async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        foreach (var scriptPath in _scriptPaths)
        {
            await ApplyScriptAsync(connection, scriptPath, cancellationToken);
        }
    }

    private async Task ApplyScriptAsync(SqliteConnection connection, string scriptPath, CancellationToken cancellationToken)
    {
        try
        {
            var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            ScriptFailed(_logger, scriptPath, exception);
            throw;
        }
    }
}
