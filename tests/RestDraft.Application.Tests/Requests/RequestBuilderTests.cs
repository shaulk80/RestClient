using System.Text;
using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Requests;
using RestDraft.Application.Settings;
using RestDraft.Domain.Auth;
using RestDraft.Domain.Bodies;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.Requests;

[TestFixture]
public class RequestBuilderTests
{
    private const string FilePath = "C:\\data\\input.txt";
    private const string Boundary = "RestDraftBoundary0123456789abcdef0123456789abcdef";

    private static readonly IReadOnlyDictionary<string, string> Variables = new Dictionary<string, string>
    {
        ["host"] = "example.com",
        ["token"] = "abc",
    };

    private static readonly Guid BoundaryId = Guid.Parse("0123456789abcdef0123456789abcdef");

    private readonly RequestBuilder _builder = RequestBuilderFactory.Create(AppSettings.Default.Timeout);

    [Test]
    public async Task BuildAppendsEnabledQueryParamsEncoded()
    {
        var definition = CreateDefinition("https://{{host}}/search") with
        {
            QueryParams =
            [
                new KeyValueEntry("q", "a b&c", true),
                new KeyValueEntry("skip", "1", false),
            ],
        };

        var result = await _builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(result.Value?.Url, Is.EqualTo("https://example.com/search?q=a%20b%26c"));
    }

    [Test]
    public async Task BuildAppendsQueryWithAmpersandWhenUrlAlreadyHasQuery()
    {
        var definition = CreateDefinition("https://example.com/a?x=1") with
        {
            QueryParams = [new KeyValueEntry("y", "2", true)],
        };

        var result = await _builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(result.Value?.Url, Is.EqualTo("https://example.com/a?x=1&y=2"));
    }

    [Test]
    public async Task BuildResolvesVariablesInHeaders()
    {
        var definition = CreateDefinition("https://example.com") with
        {
            Headers = [new KeyValueEntry("Authorization", "Bearer {{token}}", true)],
        };

        var result = await _builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(result.Value?.Headers, Does.Contain(new KeyValueEntry("Authorization", "Bearer abc", true)));
    }

    [Test]
    public async Task BuildSendsRawBodyResolvedAsUtf8()
    {
        var definition = CreateDefinition("https://example.com") with
        {
            Body = new RawBody("text/plain", "host={{host}}"),
        };

        var result = await _builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(ReadContent(result.Value), Is.EqualTo("host=example.com"));
    }

    [Test]
    public async Task BuildSetsContentTypeFromRawBodyAndReplacesExistingHeader()
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

        var result = await _builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(result.Value?.Headers, Does.Contain(new KeyValueEntry("Content-Type", "application/json", true)));
        Assert.That(result.Value?.Headers, Does.Not.Contain(new KeyValueEntry("content-type", "text/plain", true)));
    }

    [Test]
    public async Task BuildLeavesContentEmptyWhenThereIsNoBody()
    {
        var result = await _builder.BuildAsync(CreateDefinition("https://example.com"), Variables, CancellationToken.None);

        Assert.That(result.Value?.Content, Is.Null);
    }

    [Test]
    public async Task BuildUsesConfiguredTimeout()
    {
        var builder = RequestBuilderFactory.Create(TimeSpan.FromSeconds(7));

        var result = await builder.BuildAsync(CreateDefinition("https://example.com"), Variables, CancellationToken.None);

        Assert.That(result.Value?.Timeout, Is.EqualTo(TimeSpan.FromSeconds(7)));
    }

    [Test]
    public async Task BuildFailsWhenSettingsCannotBeRead()
    {
        var settings = A.Fake<ISettingsService>();
        A.CallTo(() => settings.GetAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<AppSettings>.Failure(new Error(ErrorCategory.Storage, "Unreadable."))));
        var builder = RequestBuilderFactory.Create(settings);

        var result = await builder.BuildAsync(CreateDefinition("https://example.com"), Variables, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }

    [Test]
    public async Task BuildFailsWhenVariableIsUnresolved()
    {
        var result = await _builder.BuildAsync(CreateDefinition("https://{{missing}}"), Variables, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [TestCaseSource(nameof(AuthHeaderCases))]
    public async Task BuildAddsAuthHeaderWithVariablesResolved(AuthDefinition auth, KeyValueEntry expected)
    {
        var result = await _builder.BuildAsync(CreateDefinition("https://example.com") with { Auth = auth }, Variables, CancellationToken.None);

        Assert.That(result.Value?.Headers, Does.Contain(expected));
    }

    [Test]
    public async Task BuildAddsApiKeyToQueryWhenLocationIsQuery()
    {
        var auth = new ApiKeyAuth("api_key", "{{token}}", ApiKeyLocation.Query);

        var result = await _builder.BuildAsync(CreateDefinition("https://example.com/a?x=1") with { Auth = auth }, Variables, CancellationToken.None);

        Assert.That(result.Value?.Url, Is.EqualTo("https://example.com/a?x=1&api_key=abc"));
    }

    [Test]
    public async Task BuildReplacesUserAuthorizationHeaderWithAuth()
    {
        var definition = CreateDefinition("https://example.com") with
        {
            Headers = [new KeyValueEntry("authorization", "old", true)],
            Auth = new BearerAuth("{{token}}"),
        };

        var result = await _builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(result.Value?.Headers.Select(header => header.Value).ToList(), Is.EqualTo(new List<string> { "Bearer abc" }));
    }

    [TestCaseSource(nameof(UnresolvedAuthCases))]
    public async Task BuildFailsWhenAuthVariableIsUnresolved(AuthDefinition auth)
    {
        var result = await _builder.BuildAsync(CreateDefinition("https://example.com") with { Auth = auth }, Variables, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [Test]
    public async Task BuildEncodesEnabledFormFieldsAsUrlEncoded()
    {
        var definition = CreateDefinition("https://example.com") with
        {
            Body = new FormUrlEncodedBody(
            [
                new KeyValueEntry("q", "a b&{{host}}", true),
                new KeyValueEntry("skip", "1", false),
            ]),
        };

        var result = await _builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(ReadContent(result.Value), Is.EqualTo("q=a%20b%26example.com"));
        Assert.That(result.Value?.Headers, Does.Contain(new KeyValueEntry("Content-Type", RequestBodyResolver.FormUrlEncodedContentType, true)));
    }

    [Test]
    public async Task BuildFailsWhenFormFieldVariableIsUnresolved()
    {
        var definition = CreateDefinition("https://example.com") with
        {
            Body = new FormUrlEncodedBody([new KeyValueEntry("q", "{{missing}}", true)]),
        };

        var result = await _builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [Test]
    public async Task BuildEncodesMultipartValuesAndFilesWithBoundary()
    {
        var files = A.Fake<IFileReader>();
        A.CallTo(() => files.ReadAllBytesAsync(FilePath, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<byte[]>.Success(Encoding.UTF8.GetBytes("FILE"))));
        var ids = A.Fake<IIdGenerator>();
        A.CallTo(() => ids.NewId()).Returns(BoundaryId);
        var builder = RequestBuilderFactory.Create(AppSettings.Default.Timeout, files, ids);
        var definition = CreateDefinition("https://example.com") with
        {
            Body = new MultipartBody(
            [
                new MultipartPart("title", "{{host}}", null),
                new MultipartPart("file", null, FilePath),
            ]),
        };

        var result = await builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(result.Value?.Headers, Does.Contain(new KeyValueEntry("Content-Type", $"multipart/form-data; boundary={Boundary}", true)));
        Assert.That(ReadContent(result.Value), Is.EqualTo(ExpectedMultipartContent()));
    }

    [Test]
    public async Task BuildFailsWhenMultipartFileCannotBeRead()
    {
        var files = A.Fake<IFileReader>();
        A.CallTo(() => files.ReadAllBytesAsync(FilePath, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<byte[]>.Failure(new Error(ErrorCategory.NotFound, "The file was not found."))));
        var builder = RequestBuilderFactory.Create(AppSettings.Default.Timeout, files);
        var definition = CreateDefinition("https://example.com") with
        {
            Body = new MultipartBody([new MultipartPart("file", null, FilePath)]),
        };

        var result = await builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    public static IEnumerable<TestCaseData> AuthHeaderCases()
    {
        yield return new TestCaseData(new BasicAuth("user", "{{token}}"), new KeyValueEntry("Authorization", "Basic dXNlcjphYmM=", true));
        yield return new TestCaseData(new BearerAuth("{{token}}"), new KeyValueEntry("Authorization", "Bearer abc", true));
        yield return new TestCaseData(new ApiKeyAuth("X-Key", "{{token}}", ApiKeyLocation.Header), new KeyValueEntry("X-Key", "abc", true));
    }

    public static IEnumerable<AuthDefinition> UnresolvedAuthCases()
    {
        return
        [
            new BasicAuth("{{missing}}", "secret"),
            new BearerAuth("{{missing}}"),
            new ApiKeyAuth("X-Key", "{{missing}}", ApiKeyLocation.Header),
        ];
    }

    private static string ExpectedMultipartContent()
    {
        return $"--{Boundary}\r\n"
            + "Content-Disposition: form-data; name=\"title\"\r\n\r\n"
            + "example.com\r\n"
            + $"--{Boundary}\r\n"
            + "Content-Disposition: form-data; name=\"file\"; filename=\"input.txt\"\r\n"
            + "Content-Type: application/octet-stream\r\n\r\n"
            + "FILE\r\n"
            + $"--{Boundary}--\r\n";
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
