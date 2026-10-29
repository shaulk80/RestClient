using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestDraft.Application.History;
using RestDraft.Domain.Bodies;
using RestDraft.Domain.Collections;
using RestDraft.Domain.History;
using RestDraft.Domain.Results;
using RestDraft.Infrastructure.Persistence;

namespace RestDraft.Infrastructure.Tests.History;

[TestFixture]
public class SqliteHistoryRepositoryTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 20, 12, 0, 0, 250, TimeSpan.Zero);

    private static readonly string[] NewestFirstUrls =
    [
        "https://other.test/status",
        "https://example.com/api/orders",
        "https://example.com/api/users",
    ];

    private string _connectionString = string.Empty;
    private SqliteConnection? _keepAlive;
    private SqliteHistoryRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = $"HistoryRepositoryTests{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        };
        _connectionString = builder.ConnectionString;

        // An in-memory shared database lives only while one connection stays open.
        _keepAlive = new SqliteConnection(_connectionString);
        await _keepAlive.OpenAsync();

        var migrated = await CreateMigrator().MigrateAsync(CancellationToken.None);
        Assert.That(migrated.IsSuccess, Is.True);

        _repository = new SqliteHistoryRepository(
            new SqliteConnectionFactory(_connectionString),
            NullLogger<SqliteHistoryRepository>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _keepAlive?.Dispose();
    }

    [Test]
    public async Task AddThenGetRoundTripsSuccessfulEntry()
    {
        var entry = CreateEntry(BaseTime, "https://example.com/api/users", RequestMethod.Post, 201, "temp/body.body");

        await _repository.AddAsync(entry, CancellationToken.None);
        var loaded = await _repository.GetAsync(entry.Id, CancellationToken.None);

        Assert.That(loaded.IsSuccess, Is.True);
        AssertSameEntry(loaded.Value!, entry);
    }

    [Test]
    public async Task AddThenGetRoundTripsFailedEntryWithoutStatus()
    {
        var entry = CreateEntry(BaseTime, "https://example.com", RequestMethod.Get, null, null) with
        {
            ErrorMessage = "The request timed out. (Timeout)",
        };

        await _repository.AddAsync(entry, CancellationToken.None);
        var loaded = await _repository.GetAsync(entry.Id, CancellationToken.None);

        Assert.That(loaded.Value?.StatusCode, Is.Null);
        Assert.That(loaded.Value?.ErrorMessage, Is.EqualTo("The request timed out. (Timeout)"));
        Assert.That(loaded.Value?.BodyFilePath, Is.Null);
    }

    [Test]
    public async Task GetReturnsNotFoundForMissingEntry()
    {
        var result = await _repository.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    [Test]
    public async Task SearchReturnsNewestFirst()
    {
        await SaveSampleEntriesAsync();

        var result = await _repository.SearchAsync(HistoryFilter.All, CancellationToken.None);

        Assert.That(result.Value!.Select(entry => entry.Url), Is.EqualTo(NewestFirstUrls));
    }

    [TestCase("", null, 3)]
    [TestCase("api", null, 2)]
    [TestCase("API", RequestMethod.Get, 1)]
    [TestCase("", RequestMethod.Post, 1)]
    [TestCase("status", RequestMethod.Post, 0)]
    [TestCase("nothing", null, 0)]
    public async Task SearchFiltersByUrlTextIgnoringCaseAndByMethod(string urlText, RequestMethod? method, int expectedCount)
    {
        await SaveSampleEntriesAsync();

        var result = await _repository.SearchAsync(new HistoryFilter(urlText, method), CancellationToken.None);

        Assert.That(result.Value, Has.Count.EqualTo(expectedCount));
    }

    [Test]
    public async Task DeleteRemovesOnlyTheListedEntries()
    {
        var keep = CreateEntry(BaseTime, "https://example.com/keep", RequestMethod.Get, 200, null);
        var remove = CreateEntry(BaseTime.AddSeconds(1), "https://example.com/remove", RequestMethod.Get, 200, null);
        await _repository.AddAsync(keep, CancellationToken.None);
        await _repository.AddAsync(remove, CancellationToken.None);

        var deleted = await _repository.DeleteAsync([remove.Id], CancellationToken.None);
        var remaining = await _repository.SearchAsync(HistoryFilter.All, CancellationToken.None);

        Assert.That(deleted.IsSuccess, Is.True);
        Assert.That(remaining.Value!.Select(entry => entry.Id), Is.EqualTo(new[] { keep.Id }));
    }

    [TestCase("UPDATE history_entries SET request_json = 'null';")]
    [TestCase("UPDATE history_entries SET method = 'Unknown';")]
    public async Task SearchReturnsStorageErrorWhenStoredRowIsDamaged(string damagingSql)
    {
        await SaveSampleEntriesAsync();
        await ExecuteAsync(damagingSql);

        var result = await _repository.SearchAsync(HistoryFilter.All, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }

    private static HistoryEntry CreateEntry(DateTimeOffset sentAt, string url, RequestMethod method, int? statusCode, string? bodyFilePath)
    {
        var request = new RequestDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Saved",
            Method = method,
            Url = url,
            QueryParams = [new KeyValueEntry("page", "2", true)],
            Headers = [new KeyValueEntry("Accept", "application/json", false)],
            Body = new RawBody("application/json", "{}"),
        };
        return new HistoryEntry(Guid.NewGuid(), sentAt, method, url, request, statusCode, 12.5, null, bodyFilePath);
    }

    private static void AssertSameEntry(HistoryEntry actual, HistoryEntry expected)
    {
        Assert.That(actual.Id, Is.EqualTo(expected.Id));
        Assert.That(actual.SentAt, Is.EqualTo(expected.SentAt));
        Assert.That(actual.Method, Is.EqualTo(expected.Method));
        Assert.That(actual.Url, Is.EqualTo(expected.Url));
        Assert.That(actual.StatusCode, Is.EqualTo(expected.StatusCode));
        Assert.That(actual.DurationMs, Is.EqualTo(expected.DurationMs));
        Assert.That(actual.ErrorMessage, Is.EqualTo(expected.ErrorMessage));
        Assert.That(actual.BodyFilePath, Is.EqualTo(expected.BodyFilePath));
        Assert.That(actual.RequestSnapshot.Id, Is.EqualTo(expected.RequestSnapshot.Id));
        Assert.That(actual.RequestSnapshot.Name, Is.EqualTo(expected.RequestSnapshot.Name));
        Assert.That(actual.RequestSnapshot.Url, Is.EqualTo(expected.RequestSnapshot.Url));
        Assert.That(actual.RequestSnapshot.QueryParams, Is.EqualTo(expected.RequestSnapshot.QueryParams));
        Assert.That(actual.RequestSnapshot.Headers, Is.EqualTo(expected.RequestSnapshot.Headers));
        Assert.That(actual.RequestSnapshot.Body, Is.EqualTo(expected.RequestSnapshot.Body));
    }

    private async Task SaveSampleEntriesAsync()
    {
        await _repository.AddAsync(CreateEntry(BaseTime, "https://example.com/api/users", RequestMethod.Get, 200, null), CancellationToken.None);
        await _repository.AddAsync(CreateEntry(BaseTime.AddSeconds(1), "https://example.com/api/orders", RequestMethod.Post, 201, null), CancellationToken.None);
        await _repository.AddAsync(CreateEntry(BaseTime.AddSeconds(2), "https://other.test/status", RequestMethod.Get, null, null), CancellationToken.None);
    }

    private SqliteMigrator CreateMigrator()
    {
        var scripts = new SqlScriptMigrationSource(
            Path.Combine(AppContext.BaseDirectory, "MigrationScripts"),
            NullLogger<SqlScriptMigrationSource>.Instance);
        var migrationSetBuilder = new MigrationSetBuilder(new[] { scripts }, NullLogger<MigrationSetBuilder>.Instance);
        return new SqliteMigrator(
            new SqliteConnectionFactory(_connectionString),
            migrationSetBuilder,
            NullLogger<SqliteMigrator>.Instance);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
