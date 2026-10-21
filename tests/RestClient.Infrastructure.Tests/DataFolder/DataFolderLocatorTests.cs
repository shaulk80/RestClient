using NUnit.Framework;
using RestClient.Domain.Results;
using RestClient.Infrastructure.DataFolder;

namespace RestClient.Infrastructure.Tests.DataFolder;

[TestFixture]
public class DataFolderLocatorTests
{
    private const string RootPlaceholder = "{root}";

    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "RestClientTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_root, recursive: true);
    }

    [TestCase(new[] { "--data-dir", "{root}/custom" }, null, "custom")]
    [TestCase(new[] { "--data-dir={root}/custom" }, null, "custom")]
    [TestCase(new string[] { }, "{root}/from-env", "from-env")]
    [TestCase(new[] { "--data-dir", "{root}/arg" }, "{root}/from-env", "arg")]
    [TestCase(new string[] { }, null, "data")]
    public void LocateResolvesFolderByPriority(string[] args, string? environmentValue, string expectedFolder)
    {
        var result = DataFolderLocator.Locate(
            args.Select(ResolvePlaceholder).ToArray(),
            environmentValue?.Replace(RootPlaceholder, _root, StringComparison.Ordinal),
            _root);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(Path.GetFullPath(Path.Combine(_root, expectedFolder))));
        Assert.That(Directory.Exists(result.Value), Is.True);
    }

    [Test]
    public void LocateReturnsStorageErrorWhenFolderCannotBeCreated()
    {
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, string.Empty);

        var result = DataFolderLocator.Locate([], null, blocker);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }

    private string ResolvePlaceholder(string template)
    {
        return template.Replace(RootPlaceholder, _root, StringComparison.Ordinal);
    }
}
