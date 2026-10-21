using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence;
using RestClient.Infrastructure.Persistence.Migrations;

namespace RestClient.Infrastructure.Tests.Persistence;

[TestFixture]
public class SqliteMigratorTests
{
    private string _connectionString = string.Empty;
    private SqliteConnection? _keepAlive;

    [SetUp]
    public void SetUp()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = $"MigratorTests{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        };
        _connectionString = builder.ConnectionString;

        // An in-memory shared database lives only while one connection stays open.
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _keepAlive?.Dispose();
    }

    [Test]
    public async Task MigrateAsyncCreatesSettingsTableAndSetsVersion()
    {
        var migrator = CreateMigrator([new Migration0001Settings()]);

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(await ReadScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'settings';"), Is.EqualTo(1));
        Assert.That(await ReadUserVersionAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task MigrateAsyncIsIdempotent()
    {
        var migrator = CreateMigrator([new Migration0001Settings()]);
        await migrator.MigrateAsync(CancellationToken.None);

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(await ReadUserVersionAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task MigrateAsyncReturnsStorageErrorAndKeepsVersionWhenMigrationFails()
    {
        var migrator = CreateMigrator([new BrokenMigration()]);

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
        Assert.That(await ReadUserVersionAsync(), Is.EqualTo(0));
    }

    private SqliteMigrator CreateMigrator(IEnumerable<IMigration> migrations)
    {
        return new SqliteMigrator(
            new SqliteConnectionFactory(_connectionString),
            migrations,
            NullLogger<SqliteMigrator>.Instance);
    }

    private async Task<int> ReadUserVersionAsync()
    {
        return await ReadScalarAsync("PRAGMA user_version;");
    }

    private async Task<int> ReadScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private sealed class BrokenMigration : IMigration
    {
        public int Version => 1;

        public async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE broken (;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
