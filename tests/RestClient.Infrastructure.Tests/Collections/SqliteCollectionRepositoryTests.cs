using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestClient.Domain.Auth;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence;

namespace RestClient.Infrastructure.Tests.Collections;

[TestFixture]
public class SqliteCollectionRepositoryTests
{
    private string _connectionString = string.Empty;
    private SqliteConnection? _keepAlive;
    private SqliteCollectionRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = $"CollectionRepositoryTests{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        };
        _connectionString = builder.ConnectionString;

        // An in-memory shared database lives only while one connection stays open.
        _keepAlive = new SqliteConnection(_connectionString);
        await _keepAlive.OpenAsync();

        var migrator = CreateMigrator();
        var migrated = await migrator.MigrateAsync(CancellationToken.None);
        Assert.That(migrated.IsSuccess, Is.True);

        _repository = new SqliteCollectionRepository(
            new SqliteConnectionFactory(_connectionString),
            NullLogger<SqliteCollectionRepository>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _keepAlive?.Dispose();
    }

    [Test]
    public async Task LoadReturnsEmptyListWhenNothingIsSaved()
    {
        var result = await _repository.LoadAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task SaveThenLoadRoundTripsDeepTreeWithBodyAndAuthVariants()
    {
        var workspace = CreateDeepWorkspace();

        var saved = await _repository.SaveAsync(workspace, CancellationToken.None);
        var loaded = await _repository.LoadAsync(CancellationToken.None);

        Assert.That(saved.IsSuccess, Is.True);
        Assert.That(loaded.IsSuccess, Is.True);
        Assert.That(CollectionDescriber.Describe(loaded.Value!), Is.EqualTo(CollectionDescriber.Describe(workspace)));
    }

    [Test]
    public async Task SaveReplacesPreviousWorkspace()
    {
        await _repository.SaveAsync(CreateDeepWorkspace(), CancellationToken.None);
        var replacement = new[] { CreateCollection("Replacement") };

        await _repository.SaveAsync(replacement, CancellationToken.None);
        var loaded = await _repository.LoadAsync(CancellationToken.None);

        Assert.That(CollectionDescriber.Describe(loaded.Value!), Is.EqualTo(CollectionDescriber.Describe(replacement)));
    }

    [Test]
    public async Task SaveOfEmptyListClearsWorkspace()
    {
        await _repository.SaveAsync(CreateDeepWorkspace(), CancellationToken.None);

        await _repository.SaveAsync([], CancellationToken.None);
        var loaded = await _repository.LoadAsync(CancellationToken.None);

        Assert.That(loaded.Value, Is.Empty);
    }

    [Test]
    public async Task LoadReturnsStorageErrorWhenStoredRequestIsCorrupt()
    {
        await _repository.SaveAsync(CreateDeepWorkspace(), CancellationToken.None);
        await ExecuteAsync("UPDATE requests SET data = 'null';");

        var result = await _repository.LoadAsync(CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }

    [Test]
    public async Task LoadReturnsStorageErrorWhenCollectionHasNoRootFolder()
    {
        await _repository.SaveAsync(new[] { CreateCollection("Orphan") }, CancellationToken.None);
        await ExecuteAsync("DELETE FROM folders;");

        var result = await _repository.LoadAsync(CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }

    [Test]
    public async Task DeletingFolderAndSavingRemovesItsSubtreeFromStorage()
    {
        var workspace = CreateDeepWorkspace();
        await _repository.SaveAsync(workspace, CancellationToken.None);
        var deep = workspace[0];
        var withoutMiddle = deep with { Root = deep.Root with { Folders = [deep.Root.Folders[0]] } };

        await _repository.SaveAsync([withoutMiddle, workspace[1]], CancellationToken.None);
        var loaded = await _repository.LoadAsync(CancellationToken.None);

        Assert.That(CollectionDescriber.Describe(loaded.Value!), Is.EqualTo(CollectionDescriber.Describe([withoutMiddle, workspace[1]])));
        Assert.That(await CountAsync("requests"), Is.EqualTo(1), "only the root request is left");
        Assert.That(await CountAsync("folders"), Is.EqualTo(3), "the two collection roots and First are left");
    }

    [Test]
    public async Task DeletingCollectionAndSavingRemovesItsFoldersAndRequestsFromStorage()
    {
        var workspace = CreateDeepWorkspace();
        await _repository.SaveAsync(workspace, CancellationToken.None);

        await _repository.SaveAsync([workspace[1]], CancellationToken.None);
        var loaded = await _repository.LoadAsync(CancellationToken.None);

        Assert.That(loaded.Value!.Single().Name, Is.EqualTo("Second"));
        Assert.That(await CountAsync("requests"), Is.Zero);
        Assert.That(await CountAsync("folders"), Is.EqualTo(1));
        Assert.That(await CountAsync("collections"), Is.EqualTo(1));
    }

    private static IReadOnlyList<Collection> CreateDeepWorkspace()
    {
        var leaf = new Folder(Guid.NewGuid(), "Leaf", [], [
            CreateRequest("raw", new RawBody("application/json", "{\"a\":1}"), new BearerAuth("token-1")),
            CreateRequest("form", new FormUrlEncodedBody([new KeyValueEntry("user", "me", true)]), new BasicAuth("alice", "secret")),
        ]);
        var middle = new Folder(Guid.NewGuid(), "Middle", [leaf], [
            CreateRequest("multipart", new MultipartBody([
                new MultipartPart("title", "hello", null),
                new MultipartPart("upload", null, "C:\\files\\a.bin"),
            ]), new ApiKeyAuth("X-Key", "abc", ApiKeyLocation.Query)),
        ]);
        var first = new Folder(Guid.NewGuid(), "First", [], []);
        var root = new Folder(Guid.NewGuid(), string.Empty, [first, middle], [
            CreateRequest("plain", new NoBody(), new NoAuth()) with
            {
                QueryParams = [new KeyValueEntry("page", "2", false)],
                Headers = [new KeyValueEntry("Accept", "text/plain", true)],
            },
        ]);
        return [new Collection(Guid.NewGuid(), "Deep", root), CreateCollection("Second")];
    }

    private static Collection CreateCollection(string name)
    {
        return new Collection(Guid.NewGuid(), name, new Folder(Guid.NewGuid(), string.Empty, [], []));
    }

    private static RequestDefinition CreateRequest(string name, BodyDefinition body, AuthDefinition auth)
    {
        return new RequestDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Method = RequestMethod.Post,
            Url = $"https://example.com/{name}",
            Body = body,
            Auth = auth,
        };
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

    private async Task<long> CountAsync(string tableAndFilter)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableAndFilter};";
        return (long)(await command.ExecuteScalarAsync())!;
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
