using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence;

namespace RestClient.Infrastructure.Tests.Persistence;

[TestFixture]
public class SqlScriptMigrationSourceTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
    }

    [Test]
    public void GetMigrationsOrdersFoldersByVersion()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0005", "0005_five.sql", "SELECT 1;");
        folder.AddFile("0001", "0001_one.sql", "SELECT 1;");
        folder.AddFile("0003", "0003_three.sql", "SELECT 1;");
        int[] expected = [1, 3, 5];

        var versions = GetVersions(CreateSource());

        Assert.That(versions, Is.EqualTo(expected));
    }

    [TestCase("1")]
    [TestCase("00001")]
    [TestCase("0000")]
    [TestCase("abc")]
    [TestCase("0002a")]
    public void GetMigrationsSkipsFoldersWhoseNameIsNotAVersion(string invalidFolderName)
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0001_one.sql", "SELECT 1;");
        folder.AddFile(invalidFolderName, "script.sql", "SELECT 1;");
        int[] expected = [1];

        var versions = GetVersions(CreateSource());

        Assert.That(versions, Is.EqualTo(expected));
    }

    [Test]
    public void GetMigrationsSkipsFoldersWithoutSqlFiles()
    {
        var folder = new TestMigrationFolder(_root);
        folder.AddFile("0001", "0001_one.sql", "SELECT 1;");
        folder.AddFile("0002", "readme.txt", "not a migration");
        int[] expected = [1];

        var versions = GetVersions(CreateSource());

        Assert.That(versions, Is.EqualTo(expected));
    }

    [Test]
    public void GetMigrationsReturnsFailureWhenScriptsFolderIsMissing()
    {
        var source = new SqlScriptMigrationSource(_root, NullLogger<SqlScriptMigrationSource>.Instance);

        var result = source.GetMigrations();

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }

    private SqlScriptMigrationSource CreateSource()
    {
        return new SqlScriptMigrationSource(_root, NullLogger<SqlScriptMigrationSource>.Instance);
    }

    private static List<int> GetVersions(SqlScriptMigrationSource source)
    {
        var result = source.GetMigrations();
        return result.Value?.Select(migration => migration.Version).ToList() ?? [];
    }
}
