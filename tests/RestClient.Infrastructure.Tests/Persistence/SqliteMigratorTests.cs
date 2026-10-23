using System.Globalization;
using FakeItEasy;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence;

namespace RestClient.Infrastructure.Tests.Persistence;

[TestFixture]
public class SqliteMigratorTests
{
    private string _connectionString = string.Empty;
    private string _root = string.Empty;
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
        _root = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));

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
    public async Task MigrateAsyncCreatesTableFromScriptAndSetsVersion()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0001_create_settings.sql", "CREATE TABLE settings (key TEXT NOT NULL PRIMARY KEY, value TEXT NOT NULL);");
        var migrator = CreateMigrator(CreateScriptSource());

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(await ReadScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'settings';"), Is.EqualTo(1));
        Assert.That(await ReadUserVersionAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task MigrateAsyncExecutesEveryStatementInScript()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0001_tables.sql", "CREATE TABLE first_table (id INTEGER);\nCREATE TABLE second_table (id INTEGER);");
        var migrator = CreateMigrator(CreateScriptSource());

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(await ReadScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('first_table', 'second_table');"), Is.EqualTo(2));
    }

    [Test]
    public async Task MigrateAsyncRunsScriptsOfOneVersionInFileNameOrder()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0002_insert_row.sql", "INSERT INTO items (name) VALUES ('first');");
        folder.AddFile("0001", "0001_create_items.sql", "CREATE TABLE items (name TEXT NOT NULL);");
        var migrator = CreateMigrator(CreateScriptSource());

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(await ReadScalarAsync("SELECT COUNT(*) FROM items;"), Is.EqualTo(1));
    }

    [Test]
    public async Task MigrateAsyncIsIdempotent()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0001_create_items.sql", "CREATE TABLE items (name TEXT NOT NULL);");
        var migrator = CreateMigrator(CreateScriptSource());
        await migrator.MigrateAsync(CancellationToken.None);

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(await ReadUserVersionAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task MigrateAsyncAppliesOnlyNewerVersionsOnUpgrade()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0001_first.sql", "CREATE TABLE first_table (id INTEGER);");
        var migrator = CreateMigrator(CreateScriptSource());
        await migrator.MigrateAsync(CancellationToken.None);
        folder.AddFile("0002", "0002_second.sql", "CREATE TABLE second_table (id INTEGER);");
        folder.AddFile("0004", "0004_fourth.sql", "CREATE TABLE fourth_table (id INTEGER);");

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(await ReadUserVersionAsync(), Is.EqualTo(4));
        Assert.That(await ReadScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('second_table', 'fourth_table');"), Is.EqualTo(2));
    }

    [Test]
    public async Task MigrateAsyncAppliesGapsInVersionsAndSetsMaxVersion()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0001_first.sql", "CREATE TABLE first_table (id INTEGER);");
        folder.AddFile("0003", "0003_third.sql", "CREATE TABLE third_table (id INTEGER);");
        var migrator = CreateMigrator(CreateScriptSource());

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(await ReadUserVersionAsync(), Is.EqualTo(3));
    }

    [Test]
    public async Task MigrateAsyncRollsBackWholeVersionWhenLaterScriptFails()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0001_first.sql", "CREATE TABLE first_table (id INTEGER);");
        folder.AddFile("0001", "0002_broken.sql", "CREATE TABLE broken (;");
        var migrator = CreateMigrator(CreateScriptSource());

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
        Assert.That(await ReadUserVersionAsync(), Is.EqualTo(0));
        Assert.That(await ReadScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'first_table';"), Is.EqualTo(0));
    }

    [Test]
    public async Task MigrateAsyncReturnsExceptionDetailsWhenScriptFails()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0001_broken.sql", "CREATE TABLE broken (;");
        var migrator = CreateMigrator(CreateScriptSource());

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.Error?.Details, Does.Contain("SqliteException"));
    }

    [Test]
    public async Task MigrateAsyncReturnsStorageErrorAndKeepsVersionWhenCodeMigrationFails()
    {
        var migrator = CreateMigrator(new CodeMigrationSource([new BrokenMigration()]));

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
        Assert.That(await ReadUserVersionAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task MigrateAsyncReturnsDetailsOfUnexpectedException()
    {
        var migration = A.Fake<IMigration>();
        A.CallTo(() => migration.Version).Returns(1);
        A.CallTo(() => migration.ApplyAsync(A<SqliteConnection>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("unexpected boom"));
        var migrator = CreateMigrator(new CodeMigrationSource([migration]));

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Details, Does.Contain("unexpected boom"));
    }

    [Test]
    public async Task MigrateAsyncFailsWhenVersionIsDefinedByScriptAndCode()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0001_first.sql", "CREATE TABLE first_table (id INTEGER);");
        var migrator = CreateMigrator(CreateScriptSource(), new CodeMigrationSource([new BrokenMigration()]));

        var result = await migrator.MigrateAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(await ReadUserVersionAsync(), Is.EqualTo(0));
    }

    private SqlScriptMigrationSource CreateScriptSource()
    {
        return new SqlScriptMigrationSource(_root, NullLogger<SqlScriptMigrationSource>.Instance);
    }

    private SqliteMigrator CreateMigrator(params IMigrationSource[] sources)
    {
        var migrationSetBuilder = new MigrationSetBuilder(sources, NullLogger<MigrationSetBuilder>.Instance);
        return new SqliteMigrator(
            new SqliteConnectionFactory(_connectionString),
            migrationSetBuilder,
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
