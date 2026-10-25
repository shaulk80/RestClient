using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.Requests;
using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Requests;

[TestFixture]
public class RequestSenderTests
{
    private static readonly IReadOnlyDictionary<string, string> Variables = new Dictionary<string, string>
    {
        ["host"] = "example.com",
    };

    private static readonly DateTimeOffset SentAt = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private IHistoryService _history = null!;
    private IClock _clock = null!;

    [SetUp]
    public void SetUp()
    {
        _history = A.Fake<IHistoryService>();
        A.CallTo(() => _history.RecordAsync(A<RequestAttempt>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        _clock = A.Fake<IClock>();
        A.CallTo(() => _clock.UtcNow).Returns(SentAt);
    }

    [Test]
    public async Task SendForwardsResolvedRequestToExecutor()
    {
        var executor = A.Fake<IHttpExecutor>();
        var snapshot = CreateSnapshot();
        A.CallTo(() => executor.SendAsync(A<OutgoingRequest>.That.Matches(request => request.Url == "https://example.com/a"), A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Success(snapshot)));
        var sender = CreateSender(executor);

        var result = await sender.SendAsync(CreateDefinition("https://{{host}}/a"), Variables, CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(snapshot));
    }

    [TestCase(ErrorCategory.Network)]
    [TestCase(ErrorCategory.Timeout)]
    [TestCase(ErrorCategory.Cancelled)]
    public async Task SendReturnsExecutorFailure(ErrorCategory category)
    {
        var executor = A.Fake<IHttpExecutor>();
        A.CallTo(() => executor.SendAsync(A<OutgoingRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Failure(new Error(category, "Failed."))));
        var sender = CreateSender(executor);

        var result = await sender.SendAsync(CreateDefinition("https://example.com"), Variables, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(category));
    }

    [TestCase(ErrorCategory.Network)]
    [TestCase(ErrorCategory.Timeout)]
    [TestCase(ErrorCategory.Cancelled)]
    public async Task SendRecordsEveryFailedExecution(ErrorCategory category)
    {
        var executor = A.Fake<IHttpExecutor>();
        A.CallTo(() => executor.SendAsync(A<OutgoingRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Failure(new Error(category, "Failed."))));
        var sender = CreateSender(executor);

        await sender.SendAsync(CreateDefinition("https://example.com"), Variables, CancellationToken.None);

        A.CallTo(() => _history.RecordAsync(
                A<RequestAttempt>.That.Matches(attempt => attempt.Outcome.Error != null && attempt.Outcome.Error.Category == category),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SendRecordsSuccessfulAttemptWithResolvedUrlAndTiming()
    {
        var executor = A.Fake<IHttpExecutor>();
        A.CallTo(() => executor.SendAsync(A<OutgoingRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Success(CreateSnapshot())));
        var sender = CreateSender(executor);

        await sender.SendAsync(CreateDefinition("https://{{host}}/a"), Variables, CancellationToken.None);

        A.CallTo(() => _history.RecordAsync(
                A<RequestAttempt>.That.Matches(attempt =>
                    attempt.Outcome.IsSuccess
                    && attempt.ResolvedUrl == "https://example.com/a"
                    && attempt.SentAt == SentAt
                    && attempt.DurationMs >= 0),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [TestCase("")]
    [TestCase("ftp://example.com")]
    [TestCase("{{missing}}")]
    public async Task SendFailsBeforeExecutingWhenUrlIsNotValid(string url)
    {
        var executor = A.Fake<IHttpExecutor>();
        var sender = CreateSender(executor);

        var result = await sender.SendAsync(CreateDefinition(url), Variables, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
        A.CallTo(() => executor.SendAsync(A<OutgoingRequest>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [TestCase("")]
    [TestCase("ftp://example.com")]
    [TestCase("{{missing}}")]
    public async Task SendRecordsValidationFailureWithTheUrlAsTyped(string url)
    {
        var sender = CreateSender(A.Fake<IHttpExecutor>());

        await sender.SendAsync(CreateDefinition(url), Variables, CancellationToken.None);

        A.CallTo(() => _history.RecordAsync(
                A<RequestAttempt>.That.Matches(attempt =>
                    attempt.Outcome.Error != null
                    && attempt.Outcome.Error.Category == ErrorCategory.Validation
                    && attempt.ResolvedUrl == url),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SendKeepsResultWhenHistoryWriteFails()
    {
        var executor = A.Fake<IHttpExecutor>();
        var snapshot = CreateSnapshot();
        A.CallTo(() => executor.SendAsync(A<OutgoingRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Success(snapshot)));
        A.CallTo(() => _history.RecordAsync(A<RequestAttempt>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Storage, "The history entry could not be saved."))));
        var sender = CreateSender(executor);

        var result = await sender.SendAsync(CreateDefinition("https://example.com"), Variables, CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(snapshot));
    }

    private RequestSender CreateSender(IHttpExecutor executor)
    {
        return new RequestSender(new RequestBuilder(RequestBuilder.DefaultTimeout), executor, _history, _clock);
    }

    private static RequestDefinition CreateDefinition(string url)
    {
        return new RequestDefinition
        {
            Id = Guid.Empty,
            Url = url,
        };
    }

    private static ResponseSnapshot CreateSnapshot()
    {
        return new ResponseSnapshot(200, "OK", [], "body", false, 4, null, new ResponseTiming(null, 1, 2));
    }
}
