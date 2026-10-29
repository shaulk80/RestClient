using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestDraft.Domain.Collections;
using RestDraft.Infrastructure.Persistence;
using RestDraft.Infrastructure.Persistence.Mappers;
using RestDraft.Infrastructure.Persistence.Storage;

namespace RestDraft.Infrastructure.Tests.History;

// Migration 0006 renames the history URL column and overwrites the resolved URLs that older versions stored.
[TestFixture]
public class HistoryUrlMigrationTests
{
    private const string ScriptsFolderName = "MigrationScripts";

    private string _connectionString = string.Empty;
    private SqliteConnection? _keepAlive;

    [SetUp]
    public async Task SetUp()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = $"HistoryUrlMigration{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        };
        _connectionString = builder.ConnectionString;
        _keepAlive = new SqliteConnection(_connectionString);
        await _keepAlive.OpenAsync();
        await MigrateUpToHistoryAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _keepAlive?.Dispose();
    }

    [Test]
    public async Task ResolvedUrlsAreReplacedByTheUrlAsTyped()
    {
        var request = new RequestDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Orders",
            Url = "https://{{host}}/orders",
            QueryParams =
            [
                new KeyValueEntry("token", "{{apiToken}}", true),
                new KeyValueEntry("page", "2", true),
                new KeyValueEntry("debug", "1", false),
            ],
        };
        await InsertOldEntryAsync(request, "https://example.com/orders?token=s3cret&page=2");

        await RunScriptAsync();

        Assert.That(await ReadUrlAsync(), Is.EqualTo("https://{{host}}/orders?token={{apiToken}}&page=2"));
    }

    [Test]
    public async Task EntryWithoutEnabledQueryParamsKeepsOnlyTheUrl()
    {
        var request = new RequestDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Plain",
            Url = "https://{{host}}/plain",
            QueryParams = [new KeyValueEntry("debug", "1", false)],
        };
        await InsertOldEntryAsync(request, "https://example.com/plain");

        await RunScriptAsync();

        Assert.That(await ReadUrlAsync(), Is.EqualTo("https://{{host}}/plain"));
    }

    [Test]
    public async Task OldUrlIndexIsReplacedAndTheColumnIsNamedUrl()
    {
        await RunScriptAsync();

        Assert.That(await ReadScalarAsync("SELECT COUNT(*) FROM pragma_table_info('history_entries') WHERE name = 'url'"), Is.EqualTo(1));
        Assert.That(await ReadScalarAsync("SELECT COUNT(*) FROM pragma_table_info('history_entries') WHERE name = 'resolved_url'"), Is.Zero);
        Assert.That(await ReadScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'ix_history_entries_url'"), Is.EqualTo(1));
        Assert.That(await ReadScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE name = 'ix_history_entries_resolved_url'"), Is.Zero);
    }

    private static string ScriptsPath => Path.Combine(AppContext.BaseDirectory, ScriptsFolderName);

    // Copies versions 0001 to 0003 only, which is the schema before 0006.
    private async Task MigrateUpToHistoryAsync()
    {
        var older = new TestMigrationFolderCopy(Path.Combine(Path.GetTempPath(), $"history-url-{Guid.NewGuid():N}"));
        try
        {
            foreach (var version in new[] { "0001", "0002", "0003" })
            {
                older.CopyVersion(Path.Combine(ScriptsPath, version), version);
            }

            var source = new SqlScriptMigrationSource(older.Root, NullLogger<SqlScriptMigrationSource>.Instance);
            var migrator = new SqliteMigrator(
                new SqliteConnectionFactory(_connectionString),
                new MigrationSetBuilder([source], NullLogger<MigrationSetBuilder>.Instance),
                NullLogger<SqliteMigrator>.Instance);
            var migrated = await migrator.MigrateAsync(CancellationToken.None);
            Assert.That(migrated.IsSuccess, Is.True);
        }
        finally
        {
            older.Delete();
        }
    }

    private async Task InsertOldEntryAsync(RequestDefinition request, string resolvedUrl)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO history_entries (id, sent_at, method, resolved_url, request_json, status_code, duration_ms) " +
            "VALUES ($id, 0, 'Get', $url, $request, 200, 1)";
        command.Parameters.AddWithValue("$id", Guid.NewGuid());
        command.Parameters.AddWithValue("$url", resolvedUrl);
        command.Parameters.AddWithValue("$request", RequestJson.Serialize(RequestMapper.ToDto(request)));
        await command.ExecuteNonQueryAsync();
    }

    private async Task RunScriptAsync()
    {
        var script = await File.ReadAllTextAsync(Path.Combine(ScriptsPath, "0006", "0006_history_url_as_typed.sql"));
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = script;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string> ReadUrlAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT url FROM history_entries";
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> ReadScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
