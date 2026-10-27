using NUnit.Framework;
using RestClient.Application.Settings;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Settings;

[TestFixture]
public class AppSettingsValidatorTests
{
    [Test]
    public void DefaultSettingsAreValid()
    {
        var result = AppSettingsValidator.Validate(AppSettings.Default);

        Assert.That(result.IsSuccess, Is.True);
    }

    [TestCase(1, true)]
    [TestCase(3600, true)]
    [TestCase(0, false)]
    [TestCase(-5, false)]
    [TestCase(3601, false)]
    public void TimeoutMustBeInRange(int seconds, bool expectedValid)
    {
        var result = AppSettingsValidator.Validate(new AppSettings { TimeoutSeconds = seconds });

        Assert.That(result.IsSuccess, Is.EqualTo(expectedValid));
    }

    [TestCase(1, true)]
    [TestCase(50, true)]
    [TestCase(0, false)]
    [TestCase(51, false)]
    public void RedirectLimitMustBeInRange(int limit, bool expectedValid)
    {
        var result = AppSettingsValidator.Validate(new AppSettings { MaxRedirects = limit });

        Assert.That(result.IsSuccess, Is.EqualTo(expectedValid));
    }

    [TestCase("", true)]
    [TestCase("http://proxy:8080", true)]
    [TestCase("https://proxy.example.com", true)]
    [TestCase("socks5://127.0.0.1:1080", true)]
    [TestCase("proxy:8080", false)]
    [TestCase("ftp://proxy", false)]
    [TestCase("not a url", false)]
    public void ProxyMustBeEmptyOrAbsoluteUrl(string proxyUrl, bool expectedValid)
    {
        var result = AppSettingsValidator.Validate(new AppSettings { ProxyUrl = proxyUrl });

        Assert.That(result.IsSuccess, Is.EqualTo(expectedValid));
    }

    [Test]
    public void InvalidSettingsGiveValidationError()
    {
        var result = AppSettingsValidator.Validate(new AppSettings { TimeoutSeconds = 0 });

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }
}
