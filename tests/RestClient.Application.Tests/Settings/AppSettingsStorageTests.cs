using NUnit.Framework;
using RestClient.Application.Settings;
using RestClient.Application.Tests.Secrets;

namespace RestClient.Application.Tests.Settings;

[TestFixture]
public class AppSettingsStorageTests
{
    private InMemorySettingsStore _store = null!;
    private AppSettingsStorage _storage = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new InMemorySettingsStore();
        _storage = new AppSettingsStorage(_store);
    }

    [Test]
    public async Task LoadGivesDefaultsWhenNothingIsStored()
    {
        var result = await _storage.LoadAsync(CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(AppSettings.Default));
    }

    [Test]
    public async Task SavedSettingsAreLoadedBack()
    {
        var settings = new AppSettings
        {
            TimeoutSeconds = 5,
            FollowRedirects = false,
            MaxRedirects = 3,
            VerifySsl = false,
            ProxyUrl = "http://proxy:8080",
        };

        await _storage.SaveAsync(settings, CancellationToken.None);
        var loaded = await _storage.LoadAsync(CancellationToken.None);

        Assert.That(loaded.Value, Is.EqualTo(settings));
    }

    [TestCase("http.timeoutSeconds", "abc")]
    [TestCase("http.timeoutSeconds", "-3")]
    [TestCase("http.maxRedirects", "")]
    [TestCase("http.followRedirects", "maybe")]
    [TestCase("http.verifySsl", "1")]
    public async Task UnreadableValueFallsBackToDefault(string key, string value)
    {
        _store.Values[key] = value;

        var result = await _storage.LoadAsync(CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(AppSettings.Default));
    }
}
