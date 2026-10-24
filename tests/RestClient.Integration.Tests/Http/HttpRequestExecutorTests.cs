using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestClient.Application.Requests;
using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;
using RestClient.Infrastructure;
using RestClient.Infrastructure.Http;
using RestClient.Infrastructure.Services;

namespace RestClient.Integration.Tests.Http;

[TestFixture]
public class HttpRequestExecutorTests
{
    private const int MegabyteBytes = 1024 * 1024;
    private static readonly TimeSpan GenerousTimeout = TimeSpan.FromSeconds(10);

    private TestHttpServer _server = null!;
    private string _dataFolder = string.Empty;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _server = new TestHttpServer();
        await _server.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _server.DisposeAsync();
    }

    [SetUp]
    public void SetUp()
    {
        _dataFolder = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
    }

    [Test]
    public async Task SendReturnsStatusHeadersAndBodyPreview()
    {
        var result = await SendAsync(Get(UrlFor("/ok")));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value?.StatusCode, Is.EqualTo(200));
        Assert.That(result.Value?.BodyPreview, Is.EqualTo("hello"));
        Assert.That(result.Value?.BodySizeBytes, Is.EqualTo(5));
        Assert.That(result.Value?.Headers.Any(header => header.Key == "Content-Type"), Is.True);
    }

    [Test]
    public async Task SendRecordsConnectTiming()
    {
        var result = await SendAsync(Get(UrlFor("/ok")));

        Assert.That(result.Value?.Timing.ConnectMs, Is.Not.Null);
    }

    [TestCase(true, 200)]
    [TestCase(false, 302)]
    public async Task SendFollowsRedirectsOnlyWhenEnabled(bool followRedirects, int expectedStatus)
    {
        var result = await SendAsync(Get(UrlFor("/redirect")), new HttpTransportOptions(followRedirects, 10));

        Assert.That(result.Value?.StatusCode, Is.EqualTo(expectedStatus));
    }

    [Test]
    public async Task SendReturnsTimeoutWhenServerIsSlower()
    {
        var request = Get(UrlFor("/slow")) with { Timeout = TimeSpan.FromMilliseconds(200) };

        var result = await SendAsync(request);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Timeout));
    }

    [Test]
    public async Task SendReturnsCancelledWhenCallerCancels()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await CreateExecutor(new HttpTransportOptions(true, 10))
            .SendAsync(Get(UrlFor("/slow")), cancellation.Token);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Cancelled));
    }

    [Test]
    public async Task SendReturnsNetworkErrorWhenServerIsUnreachable()
    {
        var result = await SendAsync(Get("http://127.0.0.1:1/"));

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Network));
    }

    [TestCase(MegabyteBytes / 2, false)]
    [TestCase(MegabyteBytes, false)]
    [TestCase(MegabyteBytes + 1, true)]
    [TestCase(2 * MegabyteBytes, true)]
    public async Task SendSpillsBodiesLargerThanPreviewLimitToFile(int sizeInBytes, bool expectSpilled)
    {
        var result = await SendAsync(Get(UrlFor($"/bytes/{sizeInBytes}")));

        Assert.That(result.Value?.BodySizeBytes, Is.EqualTo(sizeInBytes));
        Assert.That(result.Value?.IsPreviewTruncated, Is.EqualTo(expectSpilled));
        Assert.That(result.Value?.BodyPreview.Length, Is.LessThanOrEqualTo(MegabyteBytes));
        Assert.That(File.Exists(result.Value?.BodyFilePath), Is.EqualTo(expectSpilled));
    }

    private string UrlFor(string path)
    {
        return new Uri(_server.BaseAddress, path).ToString();
    }

    private static OutgoingRequest Get(string url)
    {
        return new OutgoingRequest(RequestMethod.Get, url, [], null, GenerousTimeout);
    }

    private Task<Result<ResponseSnapshot>> SendAsync(OutgoingRequest request, HttpTransportOptions? transportOptions = null)
    {
        return CreateExecutor(transportOptions ?? new HttpTransportOptions(true, 10))
            .SendAsync(request, CancellationToken.None);
    }

    private HttpRequestExecutor CreateExecutor(HttpTransportOptions transportOptions)
    {
        var infrastructureOptions = new InfrastructureOptions(_dataFolder);
        var bodyReader = new ResponseBodyReader(
            infrastructureOptions,
            new IdGenerator(),
            NullLogger<ResponseBodyReader>.Instance);

        return new HttpRequestExecutor(bodyReader, transportOptions, NullLogger<HttpRequestExecutor>.Instance);
    }
}
