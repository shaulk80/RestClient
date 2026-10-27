using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using RestClient.Application.Requests;
using RestClient.Application.Settings;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.Infrastructure;
using RestClient.Infrastructure.Http;
using RestClient.Infrastructure.Services;

namespace RestClient.Integration.Tests.Http;

// Each test sends with one executor and builder, changes the settings between sends, and expects a different result.
[TestFixture]
public class SettingsAppliedTests
{
    private TestHttpServer _server = null!;
    private TestHttpsServer _secureServer = null!;
    private MutableSettingsService _settings = null!;
    private RequestBuilder _builder = null!;
    private HttpRequestExecutor _executor = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _server = new TestHttpServer();
        await _server.StartAsync();
        _secureServer = new TestHttpsServer();
        await _secureServer.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _server.DisposeAsync();
        await _secureServer.DisposeAsync();
    }

    [SetUp]
    public void SetUp()
    {
        _settings = new MutableSettingsService(new AppSettings());
        var dataFolder = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
        var bodyReader = new ResponseBodyReader(
            new InfrastructureOptions(dataFolder),
            new IdGenerator(),
            NullLogger<ResponseBodyReader>.Instance);
        var resolver = new RequestBodyResolver(
            new MultipartBodyEncoder(new LocalFileReader(NullLogger<LocalFileReader>.Instance)),
            new IdGenerator());
        _builder = new RequestBuilder(_settings, resolver);
        _executor = new HttpRequestExecutor(bodyReader, _settings, NullLogger<HttpRequestExecutor>.Instance);
    }

    [Test]
    public async Task TurningRedirectsOffStopsFollowingThem()
    {
        var url = new Uri(_server.BaseAddress, "/redirect").ToString();

        var followed = await SendAsync(url);
        _settings.Current = _settings.Current with { FollowRedirects = false };
        var notFollowed = await SendAsync(url);

        Assert.That(followed.Value?.StatusCode, Is.EqualTo(200));
        Assert.That(notFollowed.Value?.StatusCode, Is.EqualTo(302));
    }

    [Test]
    public async Task LoweringRedirectLimitStopsBeforeTheEndOfTheChain()
    {
        var url = new Uri(_server.BaseAddress, "/redirect/5").ToString();

        var withinLimit = await SendAsync(url);
        _settings.Current = _settings.Current with { MaxRedirects = 2 };
        var overLimit = await SendAsync(url);

        Assert.That(withinLimit.Value?.StatusCode, Is.EqualTo(200));
        Assert.That(overLimit.Value?.StatusCode, Is.EqualTo(302));
    }

    [Test]
    public async Task ShorterTimeoutMakesTheSameRequestTimeOut()
    {
        var url = new Uri(_server.BaseAddress, "/delay/1500").ToString();

        _settings.Current = _settings.Current with { TimeoutSeconds = 10 };
        var generous = await SendAsync(url);
        _settings.Current = _settings.Current with { TimeoutSeconds = 1 };
        var strict = await SendAsync(url);

        Assert.That(generous.Value?.StatusCode, Is.EqualTo(200));
        Assert.That(strict.Error?.Category, Is.EqualTo(ErrorCategory.Timeout));
    }

    [Test]
    public async Task CertificateIsCheckedOnlyWhileVerificationIsOn()
    {
        var url = new Uri(_secureServer.BaseAddress, "/ok").ToString();

        var verified = await SendAsync(url);
        _settings.Current = _settings.Current with { VerifySsl = false };
        var unverified = await SendAsync(url);

        Assert.That(verified.Error?.Category, Is.EqualTo(ErrorCategory.Network));
        Assert.That(unverified.Value?.StatusCode, Is.EqualTo(200));
        Assert.That(unverified.Value?.BodyPreview, Is.EqualTo("secure"));
    }

    [Test]
    public async Task UnreachableProxyBlocksRequestsUntilItIsCleared()
    {
        var url = new Uri(_server.BaseAddress, "/ok").ToString();

        _settings.Current = _settings.Current with { ProxyUrl = "http://127.0.0.1:1" };
        var viaProxy = await SendAsync(url);
        _settings.Current = _settings.Current with { ProxyUrl = string.Empty };
        var direct = await SendAsync(url);

        Assert.That(viaProxy.Error?.Category, Is.EqualTo(ErrorCategory.Network));
        Assert.That(direct.Value?.StatusCode, Is.EqualTo(200));
    }

    private async Task<Result<RestClient.Domain.Responses.ResponseSnapshot>> SendAsync(string url)
    {
        var definition = new RequestDefinition { Id = Guid.NewGuid(), Name = "Test", Url = url };
        var built = await _builder.BuildAsync(definition, new Dictionary<string, string>(), CancellationToken.None);
        Assert.That(built.IsSuccess, Is.True);
        return await _executor.SendAsync(built.Value!, CancellationToken.None);
    }
}
