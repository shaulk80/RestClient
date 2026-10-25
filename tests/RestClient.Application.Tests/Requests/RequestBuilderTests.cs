using System.Text;
using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.Requests;
using RestClient.Domain.Auth;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Requests;

[TestFixture]
public class RequestBuilderTests
{
    private const string FilePath = "C:\\data\\input.txt";
    private const string Boundary = "RestClientBoundary0123456789abcdef0123456789abcdef";

    private static readonly IReadOnlyDictionary<string, string> Variables = new Dictionary<string, string>
    {
        ["host"] = "example.com",
        ["token"] = "abc",
    };

    private static readonly Guid BoundaryId = Guid.Parse("0123456789abcdef0123456789abcdef");

    private readonly RequestBuilder _builder = RequestBuilderFactory.Create(RequestBuilder.DefaultTimeout);

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
    public async Task BuildFailsWhenVariableIsUnresolved()
    {
        var result = await _builder.BuildAsync(CreateDefinition("https://{{missing}}"), Variables, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [TestCaseSource(nameof(UnsupportedAuth))]
    public async Task BuildFailsForAuthNotSupportedYet(AuthDefinition auth)
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
        var builder = RequestBuilderFactory.Create(RequestBuilder.DefaultTimeout, files, ids);
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
        var builder = RequestBuilderFactory.Create(RequestBuilder.DefaultTimeout, files);
        var definition = CreateDefinition("https://example.com") with
        {
            Body = new MultipartBody([new MultipartPart("file", null, FilePath)]),
        };

        var result = await builder.BuildAsync(definition, Variables, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
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
