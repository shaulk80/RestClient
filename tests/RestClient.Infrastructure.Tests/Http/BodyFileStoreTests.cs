using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestClient.Domain.Results;
using RestClient.Infrastructure;
using RestClient.Infrastructure.Http;

namespace RestClient.Infrastructure.Tests.Http;

[TestFixture]
public class BodyFileStoreTests
{
    private string _dataFolder = string.Empty;
    private string _spillFolder = string.Empty;
    private BodyFileStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _dataFolder = Path.Combine(Path.GetTempPath(), "RestClientTests", Guid.NewGuid().ToString("N"));
        _spillFolder = Path.Combine(_dataFolder, ResponseBodyReader.TempFolderName);
        Directory.CreateDirectory(_spillFolder);
        _store = new BodyFileStore(new InfrastructureOptions(_dataFolder), NullLogger<BodyFileStore>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_dataFolder, recursive: true);
    }

    [Test]
    public void DeleteRemovesFileInSpillFolder()
    {
        var path = Path.Combine(_spillFolder, "body.body");
        File.WriteAllText(path, "body");

        var result = _store.Delete(path);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(File.Exists(path), Is.False);
    }

    [Test]
    public void DeleteSucceedsWhenFileIsAlreadyGone()
    {
        var result = _store.Delete(Path.Combine(_spillFolder, "missing.body"));

        Assert.That(result.IsSuccess, Is.True);
    }

    [TestCase("app.db")]
    [TestCase("temp/../app.db")]
    [TestCase("temp-other/body.body")]
    public void DeleteRefusesPathOutsideSpillFolderAndKeepsTheFile(string relativePath)
    {
        var path = Path.Combine(_dataFolder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "keep");

        var result = _store.Delete(path);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Security));
        Assert.That(File.Exists(path), Is.True);
    }
}
