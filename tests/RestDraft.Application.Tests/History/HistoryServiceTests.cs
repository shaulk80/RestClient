using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Abstractions;
using RestDraft.Application.History;
using RestDraft.Application.Requests;
using RestDraft.Domain.Collections;
using RestDraft.Domain.History;
using RestDraft.Domain.Responses;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.History;

[TestFixture]
public class HistoryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid EntryId = Guid.NewGuid();

    private IHistoryRepository _repository = null!;
    private IBodyFileStore _bodyFiles = null!;
    private IClock _clock = null!;
    private IIdGenerator _idGenerator = null!;
    private HistoryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = A.Fake<IHistoryRepository>();
        _bodyFiles = A.Fake<IBodyFileStore>();
        _clock = A.Fake<IClock>();
        _idGenerator = A.Fake<IIdGenerator>();
        A.CallTo(() => _clock.UtcNow).Returns(Now);
        A.CallTo(() => _idGenerator.NewId()).Returns(EntryId);
        A.CallTo(() => _repository.AddAsync(A<HistoryEntry>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        A.CallTo(() => _repository.SearchAsync(A<HistoryFilter>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<HistoryEntry>>.Success([])));
        var retention = new HistoryRetention(_repository, _bodyFiles, RetentionPolicy.Default);
        _service = new HistoryService(_repository, retention, _idGenerator, _clock);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public async Task RecordSavesSuccessfulAttemptWithStatusAndBodyFile()
    {
        var attempt = CreateAttempt(Result<ResponseSnapshot>.Success(CreateSnapshot("temp/body.body")));

        var result = await _service.RecordAsync(attempt, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        A.CallTo(() => _repository.AddAsync(
                A<HistoryEntry>.That.Matches(entry =>
                    entry.Id == EntryId
                    && entry.StatusCode == 200
                    && entry.BodyFilePath == "temp/body.body"
                    && entry.ErrorMessage == null
                    && entry.Url == "https://example.com/a"
                    && entry.Method == RequestMethod.Post
                    && entry.SentAt == Now
                    && entry.DurationMs == 12.5),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [TestCase(null, "The request could not reach the server.")]
    [TestCase("", "The request could not reach the server.")]
    [TestCase("Connection refused", "The request could not reach the server. (Connection refused)")]
    public async Task RecordSavesFailedAttemptWithoutStatusAndWithErrorText(string? details, string expectedMessage)
    {
        var error = new Error(ErrorCategory.Network, "The request could not reach the server.", details);

        await _service.RecordAsync(CreateAttempt(Result<ResponseSnapshot>.Failure(error)), CancellationToken.None);

        A.CallTo(() => _repository.AddAsync(
                A<HistoryEntry>.That.Matches(entry =>
                    entry.StatusCode == null
                    && entry.BodyFilePath == null
                    && entry.ErrorMessage == expectedMessage),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RecordReturnsRepositoryFailureWithoutRunningRetention()
    {
        var failure = new Error(ErrorCategory.Storage, "The history entry could not be saved.");
        A.CallTo(() => _repository.AddAsync(A<HistoryEntry>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(failure)));

        var result = await _service.RecordAsync(CreateAttempt(Result<ResponseSnapshot>.Success(CreateSnapshot(null))), CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(failure));
        A.CallTo(() => _repository.SearchAsync(A<HistoryFilter>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task RecordRunsRetentionAfterInsert()
    {
        await _service.RecordAsync(CreateAttempt(Result<ResponseSnapshot>.Success(CreateSnapshot(null))), CancellationToken.None);

        A.CallTo(() => _repository.SearchAsync(HistoryFilter.All, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [TestCase("", null)]
    [TestCase("api", null)]
    [TestCase("", RequestMethod.Post)]
    public async Task SearchPassesFilterToRepository(string urlText, RequestMethod? method)
    {
        var filter = new HistoryFilter(urlText, method);

        await _service.SearchAsync(filter, CancellationToken.None);

        A.CallTo(() => _repository.SearchAsync(filter, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task RestoreReturnsUnsavedCopyWithNewIdAndSameContent()
    {
        var entry = HistoryFixtures.Entry(Now);
        A.CallTo(() => _repository.GetAsync(entry.Id, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<HistoryEntry>.Success(entry)));

        var result = await _service.RestoreAsync(entry.Id, CancellationToken.None);

        Assert.That(result.Value?.Id, Is.EqualTo(EntryId));
        Assert.That(result.Value?.Id, Is.Not.EqualTo(entry.RequestSnapshot.Id));
        Assert.That(result.Value?.Name, Is.EqualTo(entry.RequestSnapshot.Name));
        Assert.That(result.Value?.Url, Is.EqualTo(entry.RequestSnapshot.Url));
    }

    [Test]
    public async Task RestoreReturnsNotFoundForMissingEntry()
    {
        A.CallTo(() => _repository.GetAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<HistoryEntry>.Failure(ResultErrors.NotFound("The history entry"))));

        var result = await _service.RestoreAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    private static RequestAttempt CreateAttempt(Result<ResponseSnapshot> outcome)
    {
        var request = new RequestDefinition { Id = Guid.NewGuid(), Method = RequestMethod.Post, Url = "https://example.com/a" };
        return new RequestAttempt(request, "https://example.com/a", Now, 12.5, outcome);
    }

    private static ResponseSnapshot CreateSnapshot(string? bodyFilePath)
    {
        return new ResponseSnapshot(200, "OK", [], "body", false, 4, bodyFilePath, new ResponseTiming(null, 1, 2));
    }
}
