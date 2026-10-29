using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Abstractions;
using RestDraft.Application.History;
using RestDraft.Domain.History;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.History;

[TestFixture]
public class HistoryRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private IHistoryRepository _repository = null!;
    private IBodyFileStore _bodyFiles = null!;
    private List<HistoryEntry> _entries = null!;
    private HistoryRetention _retention = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = A.Fake<IHistoryRepository>();
        _bodyFiles = A.Fake<IBodyFileStore>();
        _entries = [];
        A.CallTo(() => _repository.SearchAsync(A<HistoryFilter>._, A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult(Result<IReadOnlyList<HistoryEntry>>.Success(_entries)));
        A.CallTo(() => _repository.DeleteAsync(A<IReadOnlyList<Guid>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        A.CallTo(() => _bodyFiles.Delete(A<string>._)).Returns(Result.Success());
        _retention = new HistoryRetention(_repository, _bodyFiles, new RetentionPolicy(1, TimeSpan.FromDays(30)));
    }

    [Test]
    public async Task ApplyDeletesExpiredEntriesAndTheirBodyFiles()
    {
        var newest = HistoryFixtures.Entry(Now.AddHours(-1), "temp/new.body");
        var oldest = HistoryFixtures.Entry(Now.AddHours(-3), "temp/old.body");
        _entries = [newest, oldest];

        var result = await _retention.ApplyAsync(Now, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        A.CallTo(() => _repository.DeleteAsync(
                A<IReadOnlyList<Guid>>.That.Matches(ids => ids.SequenceEqual(new[] { oldest.Id })),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _bodyFiles.Delete("temp/old.body")).MustHaveHappenedOnceExactly();
        A.CallTo(() => _bodyFiles.Delete("temp/new.body")).MustNotHaveHappened();
    }

    [Test]
    public async Task ApplyDoesNothingWhileEntriesAreWithinLimits()
    {
        _entries = [HistoryFixtures.Entry(Now.AddHours(-1), "temp/new.body")];

        var result = await _retention.ApplyAsync(Now, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        A.CallTo(() => _repository.DeleteAsync(A<IReadOnlyList<Guid>>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _bodyFiles.Delete(A<string>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ApplySkipsExpiredEntriesWithoutBodyFile()
    {
        _entries = [HistoryFixtures.Entry(Now.AddHours(-1)), HistoryFixtures.Entry(Now.AddHours(-3))];

        await _retention.ApplyAsync(Now, CancellationToken.None);

        A.CallTo(() => _bodyFiles.Delete(A<string>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ApplyReturnsSearchFailureWithoutDeleting()
    {
        A.CallTo(() => _repository.SearchAsync(A<HistoryFilter>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<HistoryEntry>>.Failure(new Error(ErrorCategory.Storage, "Failed."))));

        var result = await _retention.ApplyAsync(Now, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
        A.CallTo(() => _repository.DeleteAsync(A<IReadOnlyList<Guid>>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ApplyReturnsDeleteFailureWithoutDeletingFiles()
    {
        _entries = [HistoryFixtures.Entry(Now.AddHours(-1)), HistoryFixtures.Entry(Now.AddHours(-3), "temp/old.body")];
        A.CallTo(() => _repository.DeleteAsync(A<IReadOnlyList<Guid>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Storage, "Failed."))));

        var result = await _retention.ApplyAsync(Now, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
        A.CallTo(() => _bodyFiles.Delete(A<string>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ApplyDeletesEveryBodyFileAndReturnsFirstFailure()
    {
        _entries = [
            HistoryFixtures.Entry(Now.AddHours(-1)),
            HistoryFixtures.Entry(Now.AddHours(-3), "temp/a.body"),
            HistoryFixtures.Entry(Now.AddHours(-4), "temp/b.body"),
        ];
        A.CallTo(() => _bodyFiles.Delete("temp/a.body"))
            .Returns(Result.Failure(new Error(ErrorCategory.Storage, "Locked.")));

        var result = await _retention.ApplyAsync(Now, CancellationToken.None);

        Assert.That(result.Error?.Message, Is.EqualTo("Locked."));
        A.CallTo(() => _bodyFiles.Delete("temp/b.body")).MustHaveHappenedOnceExactly();
    }
}
