using NUnit.Framework;
using RestDraft.Application.Settings;
using RestDraft.Application.Tests.Secrets;

namespace RestDraft.Application.Tests.Settings;

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
            OpenOnSingleClick = true,
            Theme = ThemePreference.Dark,
            SidebarWidth = 300,
            ResponseWidth = 500,
            SidebarVisible = false,
            ResponseVisible = false,
            TopBarVisible = false,
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
    [TestCase("ui.openOnSingleClick", "yes")]
    [TestCase("ui.theme", "Purple")]
    [TestCase("ui.theme", "7")]
    [TestCase("ui.theme", "")]
    [TestCase("ui.sidebarWidth", "wide")]
    [TestCase("ui.responseWidth", "-1")]
    [TestCase("ui.sidebarVisible", "0")]
    [TestCase("ui.responseVisible", "")]
    [TestCase("ui.topBarVisible", "hidden")]
    public async Task UnreadableValueFallsBackToDefault(string key, string value)
    {
        _store.Values[key] = value;

        var result = await _storage.LoadAsync(CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(AppSettings.Default));
    }
}
