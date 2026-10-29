using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestDraft.Domain.Results;
using RestDraft.Infrastructure.Services;

namespace RestDraft.Infrastructure.Tests.Services;

[TestFixture]
public class LocalFileReaderTests
{
    private string _folder = string.Empty;
    private LocalFileReader _reader = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "RestDraftTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _reader = new LocalFileReader(NullLogger<LocalFileReader>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Test]
    public async Task ReadReturnsFileBytes()
    {
        var path = Path.Combine(_folder, "input.bin");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);

        byte[] expected = [1, 2, 3];

        var result = await _reader.ReadAllBytesAsync(path, CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(expected));
    }

    [Test]
    public async Task ReadReturnsNotFoundForMissingFile()
    {
        var path = Path.Combine(_folder, "missing.bin");

        var result = await _reader.ReadAllBytesAsync(path, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }
}
