using System.Text;
using NUnit.Framework;
using RestClient.Application.Requests;
using RestClient.Domain.Auth;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Requests;

[TestFixture]
public class RequestBuilderTests
{
    private static readonly IReadOnlyDictionary<string, string> Variables = new Dictionary<string, string>
    {
        ["host"] = "example.com",
        ["token"] = "abc",
    };

    private readonly RequestBuilder _builder = new(RequestBuilder.DefaultTimeout);

    [Test]
    public void BuildAppendsEnabledQueryParamsEncoded()
    {
        var definition = CreateDefinition("https://{{host}}/search") with
        {
            QueryParams =
            [
                new KeyValueEntry("q", "a b&c", true),
                new KeyValueEntry("skip", "1", false),
            ],
        };

        var result = _builder.Build(definition, Variables);

        Assert.That(result.Value?.Url, Is.EqualTo("https://example.com/search?q=a%20b%26c"));
    }

    [Test]
    public void BuildAppendsQueryWithAmpersandWhenUrlAlreadyHasQuery()
    {
        var definition = CreateDefinition("https://example.com/a?x=1") with
        {
            QueryParams = [new KeyValueEntry("y", "2", true)],
        };

        var result = _builder.Build(definition, Variables);

        Assert.That(result.Value?.Url, Is.EqualTo("https://example.com/a?x=1&y=2"));
    }

    [Test]
    public void BuildResolvesVariablesInHeaders()
    {
        var definition = CreateDefinition("https://example.com") with
        {
            Headers = [new KeyValueEntry("Authorization", "Bearer {{token}}", true)],
        };

        var result = _builder.Build(definition, Variables);

        Assert.That(result.Value?.Headers, Does.Contain(new KeyValueEntry("Authorization", "Bearer abc", true)));
    }

    [Test]
    public void BuildSendsRawBodyResolvedAsUtf8()
    {
        var definition = CreateDefinition("https://example.com") with
        {
            Body = new RawBody("text/plain", "host={{host}}"),
        };

        var result = _builder.Build(definition, Variables);

        Assert.That(ReadContent(result.Value), Is.EqualTo("host=example.com"));
    }

    [Test]
    public void BuildSetsContentTypeFromRawBodyAndReplacesExistingHeader()
    {
        var definition = CreateDefinition("https://example.com") with
        {
            Headers =
            [
                new KeyValueEntry("content-type", "text/plain", true),
                new KeyValueEntry("X-Id", "1", true),
            ],
            Body = new RawBody("application/json", "{}"),
        };

        var result = _builder.Build(definition, Variables);

        Assert.That(result.Value?.Headers, Does.Contain(new KeyValueEntry("Content-Type", "application/json", true)));
        Assert.That(result.Value?.Headers, Does.Not.Contain(new KeyValueEntry("content-type", "text/plain", true)));
    }

    [Test]
    public void BuildLeavesContentEmptyWhenThereIsNoBody()
    {
        var result = _builder.Build(CreateDefinition("https://example.com"), Variables);

        Assert.That(result.Value?.Content, Is.Null);
    }

    [Test]
    public void BuildUsesConfiguredTimeout()
    {
        var builder = new RequestBuilder(TimeSpan.FromSeconds(7));

        var result = builder.Build(CreateDefinition("https://example.com"), Variables);

        Assert.That(result.Value?.Timeout, Is.EqualTo(TimeSpan.FromSeconds(7)));
    }

    [Test]
    public void BuildFailsWhenVariableIsUnresolved()
    {
        var result = _builder.Build(CreateDefinition("https://{{missing}}"), Variables);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [TestCaseSource(nameof(UnsupportedBodies))]
    public void BuildFailsForBodyTypesNotSupportedYet(BodyDefinition body)
    {
        var result = _builder.Build(CreateDefinition("https://example.com") with { Body = body }, Variables);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [TestCaseSource(nameof(UnsupportedAuth))]
    public void BuildFailsForAuthNotSupportedYet(AuthDefinition auth)
    {
        var result = _builder.Build(CreateDefinition("https://example.com") with { Auth = auth }, Variables);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    public static IEnumerable<BodyDefinition> UnsupportedBodies()
    {
        return
        [
            new FormUrlEncodedBody([new KeyValueEntry("field", "value", true)]),
            new MultipartBody([new MultipartPart("file", null, "C:\\a.txt")]),
        ];
    }

    public static IEnumerable<AuthDefinition> UnsupportedAuth()
    {
        return
        [
            new BasicAuth("user", "secret"),
            new BearerAuth("token"),
            new ApiKeyAuth("X-Key", "value", ApiKeyLocation.Header),
        ];
    }

    private static RequestDefinition CreateDefinition(string url)
    {
        return new RequestDefinition
        {
            Id = Guid.Empty,
            Url = url,
        };
    }

    private static string? ReadContent(OutgoingRequest? request)
    {
        return request?.Content is { } content ? Encoding.UTF8.GetString(content.Span) : null;
    }
}
