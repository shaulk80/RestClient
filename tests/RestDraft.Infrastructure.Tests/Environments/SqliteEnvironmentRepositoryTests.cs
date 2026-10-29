using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestDraft.Application.Abstractions;
using RestDraft.Domain.Environments;
using RestDraft.Infrastructure.Persistence;

namespace RestDraft.Infrastructure.Tests.Environments;

[TestFixture]
public class SqliteEnvironmentRepositoryTests
{
    private const string SecretPlainText = "token-123";
    private const string SecretCipherText = "ciphertext-value";

    private string _connectionString = string.Empty;
    private SqliteConnection? _keepAlive;
    private SqliteEnvironmentRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = $"EnvironmentRepositoryTests{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        };
        _connectionString = builder.ConnectionString;

        // An in-memory shared database lives only while one connection stays open.
        _keepAlive = new SqliteConnection(_connectionString);
        await _keepAlive.OpenAsync();

        var migrated = await CreateMigrator().MigrateAsync(CancellationToken.None);
        Assert.That(migrated.IsSuccess, Is.True);

        _repository = new SqliteEnvironmentRepository(
            new SqliteConnectionFactory(_connectionString),
            NullLogger<SqliteEnvironmentRepository>.Instance);
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

        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task SaveThenLoadRoundTripsEnvironmentsAndVariables()
    {
        var environments = new List<RequestEnvironment>
        {
            new(Guid.NewGuid(), "Staging", [new Variable("host", "staging.example.com", false), new Variable("token", SecretCipherText, true)]),
            new(Guid.NewGuid(), "Empty", []),
        };

        await _repository.SaveAsync(environments, CancellationToken.None);
        var loaded = await _repository.LoadAsync(CancellationToken.None);

        // Records compare their list fields by reference, so the variables are compared element by element.
        Assert.That(loaded.Value?.Select(environment => environment.Name).ToList(), Is.EqualTo(new List<string> { "Staging", "Empty" }));
        Assert.That(loaded.Value?[0].Variables, Is.EqualTo(environments[0].Variables));
    }

    [Test]
    public async Task SecretIsStoredOnlyAsCiphertext()
    {
        var environment = new RequestEnvironment(Guid.NewGuid(), "Staging", [new Variable("token", SecretCipherText, true)]);

        await _repository.SaveAsync([environment], CancellationToken.None);
        var stored = await ReadSecretColumnsAsync("token");

        Assert.That(stored.Value, Is.Null);
        Assert.That(stored.SecretValue, Is.EqualTo(SecretCipherText));
    }

    [Test]
    public async Task SaveReplacesPreviousEnvironments()
    {
        await _repository.SaveAsync([new RequestEnvironment(Guid.NewGuid(), "Old", [])], CancellationToken.None);

        await _repository.SaveAsync([new RequestEnvironment(Guid.NewGuid(), "New", [])], CancellationToken.None);
        var loaded = await _repository.LoadAsync(CancellationToken.None);

        Assert.That(loaded.Value?.Select(environment => environment.Name).ToList(), Is.EqualTo(new List<string> { "New" }));
    }

    private async Task<SecretColumns> ReadSecretColumnsAsync(string name)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value, secret_value FROM environment_variables WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new SecretColumns(reader.IsDBNull(0) ? null : reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private SqliteMigrator CreateMigrator()
    {
        var scripts = new SqlScriptMigrationSource(
            Path.Combine(AppContext.BaseDirectory, "MigrationScripts"),
            NullLogger<SqlScriptMigrationSource>.Instance);
        var migrationSetBuilder = new MigrationSetBuilder([scripts], NullLogger<MigrationSetBuilder>.Instance);
        return new SqliteMigrator(
            new SqliteConnectionFactory(_connectionString),
            migrationSetBuilder,
            NullLogger<SqliteMigrator>.Instance);
    }

    private sealed record SecretColumns(string? Value, string? SecretValue);
}
