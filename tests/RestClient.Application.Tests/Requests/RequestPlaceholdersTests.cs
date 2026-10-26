using NUnit.Framework;
using RestClient.Application.Requests;
using RestClient.Domain.Auth;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;

namespace RestClient.Application.Tests.Requests;

[TestFixture]
public class RequestPlaceholdersTests
{
    [TestCaseSource(nameof(PlaceholderLocations))]
    public void NamesInFindsPlaceholderInEveryLocation(RequestDefinition definition, string expected)
    {
        Assert.That(RequestPlaceholders.NamesIn(definition), Does.Contain(expected));
    }

    [Test]
    public void NamesInIgnoresTextWithoutPlaceholders()
    {
        var definition = new RequestDefinition { Id = Guid.Empty, Url = "https://example.com" };

        Assert.That(RequestPlaceholders.NamesIn(definition), Is.Empty);
    }

    public static IEnumerable<TestCaseData> PlaceholderLocations()
    {
        var empty = new RequestDefinition { Id = Guid.Empty };
        yield return new TestCaseData(empty with { Url = "https://{{url}}" }, "url");
        yield return new TestCaseData(empty with { QueryParams = [new KeyValueEntry("q", "{{query}}", true)] }, "query");
        yield return new TestCaseData(empty with { Headers = [new KeyValueEntry("H", "{{header}}", true)] }, "header");
        yield return new TestCaseData(empty with { Body = new RawBody("text/plain", "{{raw}}") }, "raw");
        yield return new TestCaseData(empty with { Body = new FormUrlEncodedBody([new KeyValueEntry("f", "{{form}}", true)]) }, "form");
        yield return new TestCaseData(empty with { Body = new MultipartBody([new MultipartPart("p", "{{part}}", null)]) }, "part");
        yield return new TestCaseData(empty with { Auth = new BasicAuth("{{user}}", "x") }, "user");
        yield return new TestCaseData(empty with { Auth = new BearerAuth("{{token}}") }, "token");
        yield return new TestCaseData(empty with { Auth = new ApiKeyAuth("K", "{{key}}", ApiKeyLocation.Query) }, "key");
    }
}
