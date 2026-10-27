using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestClient.Application.Tabs;
using RestClient.Domain.Auth;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;
using RestClient.Infrastructure.Persistence;

namespace RestClient.Infrastructure.Tests.Persistence;

[TestFixture]
public class SqliteOpenTabsStoreTests
{
    private string _connectionString = string.Empty;
    private SqliteConnection? _keepAlive;
    private SqliteOpenTabsStore _store = null!;

    [SetUp]
    public async Task SetUp()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = $"OpenTabsStoreTests{Guid.NewGuid():N}",
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

        _store = new SqliteOpenTabsStore(new SqliteConnectionFactory(_connectionString), NullLogger<SqliteOpenTabsStore>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _keepAlive?.Dispose();
    }

    [Test]
    public async Task LoadReturnsEmptyListWhenNothingIsStored()
    {
        var result = await _store.LoadAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task SavedTabsAreLoadedBackInOrder()
    {
        var clean = new OpenTab(Guid.NewGuid(), IsLinked: true, IsSelected: false, Draft: null);
        var unsaved = new OpenTab(Guid.NewGuid(), IsLinked: false, IsSelected: true, Draft: CreateDraft());

        await _store.SaveAsync([clean, unsaved], CancellationToken.None);
        var loaded = await _store.LoadAsync(CancellationToken.None);

        Assert.That(loaded.Value, Has.Count.EqualTo(2));
        Assert.That(loaded.Value![0], Is.EqualTo(clean));
        Assert.That(loaded.Value![1].IsSelected, Is.True);
        Assert.That(loaded.Value![1].IsLinked, Is.False);
    }

    [Test]
    public async Task DraftSurvivesTheRoundTrip()
    {
        var draft = CreateDraft();

        await _store.SaveAsync([new OpenTab(draft.Id, IsLinked: false, IsSelected: false, draft)], CancellationToken.None);
        var loaded = (await _store.LoadAsync(CancellationToken.None)).Value![0].Draft!;

        Assert.That(loaded.Id, Is.EqualTo(draft.Id));
        Assert.That(loaded.Name, Is.EqualTo(draft.Name));
        Assert.That(loaded.Method, Is.EqualTo(draft.Method));
        Assert.That(loaded.Url, Is.EqualTo(draft.Url));
        Assert.That(loaded.Body, Is.EqualTo(draft.Body));
        Assert.That(loaded.Auth, Is.EqualTo(draft.Auth));
        Assert.That(loaded.Headers.Select(header => header.Key), Is.EqualTo(draft.Headers.Select(header => header.Key)));
    }

    [Test]
    public async Task SaveReplacesThePreviousTabs()
    {
        await _store.SaveAsync(
            [
                new OpenTab(Guid.NewGuid(), IsLinked: true, IsSelected: false, Draft: null),
                new OpenTab(Guid.NewGuid(), IsLinked: true, IsSelected: false, Draft: null),
            ],
            CancellationToken.None);
        var remaining = new OpenTab(Guid.NewGuid(), IsLinked: true, IsSelected: true, Draft: null);

        await _store.SaveAsync([remaining], CancellationToken.None);
        var loaded = await _store.LoadAsync(CancellationToken.None);

        Assert.That(loaded.Value, Is.EqualTo(new[] { remaining }));
    }

    [Test]
    public async Task SavingNoTabsClearsTheStore()
    {
        await _store.SaveAsync([new OpenTab(Guid.NewGuid(), IsLinked: true, IsSelected: false, Draft: null)], CancellationToken.None);

        await _store.SaveAsync([], CancellationToken.None);
        var loaded = await _store.LoadAsync(CancellationToken.None);

        Assert.That(loaded.Value, Is.Empty);
    }

    [Test]
    public async Task LoadSkipsTabWhoseDraftCannotBeRead()
    {
        var good = new OpenTab(Guid.NewGuid(), IsLinked: true, IsSelected: false, Draft: null);
        await _store.SaveAsync([good], CancellationToken.None);
        await InsertRawTabAsync(position: 1, draft: "{ not json");

        var loaded = await _store.LoadAsync(CancellationToken.None);

        Assert.That(loaded.IsSuccess, Is.True);
        Assert.That(loaded.Value, Is.EqualTo(new[] { good }));
    }

    private static RequestDefinition CreateDraft()
    {
        return new RequestDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Draft",
            Method = RequestMethod.Post,
            Url = "https://example.com/items",
            Headers = [new KeyValueEntry("X-Test", "1", true)],
            Body = new RawBody("application/json", "{ \"a\": 1 }"),
            Auth = new BearerAuth("{{token}}"),
        };
    }

    private async Task InsertRawTabAsync(int position, string draft)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO open_tabs (position, request_id, is_linked, is_selected, draft) " +
            "VALUES ($position, $id, 0, 0, $draft);";
        command.Parameters.AddWithValue("$position", position);
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$draft", draft);
        await command.ExecuteNonQueryAsync();
    }
}
