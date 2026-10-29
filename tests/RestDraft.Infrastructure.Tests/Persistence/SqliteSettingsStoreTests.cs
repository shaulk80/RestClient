using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestDraft.Infrastructure.Persistence;

namespace RestDraft.Infrastructure.Tests.Persistence;

[TestFixture]
public class SqliteSettingsStoreTests
{
    private const string Key = "secrets.salt";

    private string _connectionString = string.Empty;
    private SqliteConnection? _keepAlive;
    private SqliteSettingsStore _store = null!;

    [SetUp]
    public async Task SetUp()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = $"SettingsStoreTests{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        };
        _connectionString = builder.ConnectionString;

        // An in-memory shared database lives only while one connection stays open.
        _keepAlive = new SqliteConnection(_connectionString);
        await _keepAlive.OpenAsync();

        var scripts = new SqlScriptMigrationSource(
            Path.Combine(AppContext.BaseDirectory, "MigrationScripts"),
            NullLogger<SqlScriptMigrationSource>.Instance);
        var migrator = new SqliteMigrator(
            new SqliteConnectionFactory(_connectionString),
            new MigrationSetBuilder([scripts], NullLogger<MigrationSetBuilder>.Instance),
            NullLogger<SqliteMigrator>.Instance);
        var migrated = await migrator.MigrateAsync(CancellationToken.None);
        Assert.That(migrated.IsSuccess, Is.True);

        _store = new SqliteSettingsStore(new SqliteConnectionFactory(_connectionString), NullLogger<SqliteSettingsStore>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _keepAlive?.Dispose();
    }

    [Test]
    public async Task GetReturnsNullForMissingKey()
    {
        var result = await _store.GetAsync(Key, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Null);
    }

    [Test]
    public async Task SetThenGetReturnsValue()
    {
        await _store.SetAsync(Key, "abc", CancellationToken.None);

        var result = await _store.GetAsync(Key, CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo("abc"));
    }

    [Test]
    public async Task SetOverwritesExistingValue()
    {
        await _store.SetAsync(Key, "first", CancellationToken.None);

        await _store.SetAsync(Key, "second", CancellationToken.None);
        var result = await _store.GetAsync(Key, CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo("second"));
    }
}
