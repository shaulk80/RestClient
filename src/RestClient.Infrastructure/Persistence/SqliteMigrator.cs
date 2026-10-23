using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using RestClient.Domain.Results;

namespace RestClient.Infrastructure.Persistence;

public sealed class SqliteMigrator
{
    private const string ReadVersionSql = "PRAGMA user_version;";

    private static readonly Action<ILogger, Exception?> MigrationFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1, nameof(MigrationFailed)),
        "Database migration failed");

    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly MigrationSetBuilder _migrationSetBuilder;
    private readonly ILogger<SqliteMigrator> _logger;

    public SqliteMigrator(SqliteConnectionFactory connectionFactory, MigrationSetBuilder migrationSetBuilder, ILogger<SqliteMigrator> logger)
    {
        _connectionFactory = connectionFactory;
        _migrationSetBuilder = migrationSetBuilder;
        _logger = logger;
    }

    public async Task<Result> MigrateAsync(CancellationToken cancellationToken)
    {
        var migrationSet = _migrationSetBuilder.Build();
        if (migrationSet.Error is { } setError)
        {
            return Result.Failure(setError);
        }

        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            var currentVersion = await ReadVersionAsync(connection, cancellationToken);
            foreach (var migration in SelectPending(migrationSet.Value ?? [], currentVersion))
            {
                await ApplyAsync(connection, migration, cancellationToken);
            }

            return Result.Success();
        }
        catch (Exception exception)
        {
            MigrationFailed(_logger, exception);
            return Result.Failure(new Error(ErrorCategory.Storage, "The database could not be migrated.", exception.ToString()));
        }
    }

    private static List<IMigration> SelectPending(IReadOnlyList<IMigration> migrations, int currentVersion)
    {
        return migrations.Where(migration => migration.Version > currentVersion).ToList();
    }

    private static async Task<int> ReadVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ReadVersionSql;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task ApplyAsync(SqliteConnection connection, IMigration migration, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await migration.ApplyAsync(connection, cancellationToken);
        await SetVersionAsync(connection, migration.Version, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SetVersionAsync(SqliteConnection connection, int version, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {version.ToString(CultureInfo.InvariantCulture)};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
