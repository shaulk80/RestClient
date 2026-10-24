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

    private static RequestSender CreateSender(IHttpExecutor executor)
    {
        return new RequestSender(new RequestBuilder(RequestBuilder.DefaultTimeout), executor);
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
