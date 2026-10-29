using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Settings;
using RestDraft.Domain.Results;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

[TestFixture]
public class WorkspaceLayoutViewModelTests
{
    private ISettingsService _settings = null!;
    private WorkspaceLayoutViewModel _layout = null!;

    [SetUp]
    public void SetUp()
    {
        _settings = SettingsServiceFactory.Create(AppSettings.Default with { TimeoutSeconds = 12, OpenOnSingleClick = true });
        _layout = new WorkspaceLayoutViewModel(_settings);
    }

    [Test]
    public void StartsWithTheDefaultWidthsAndBothPanelsShown()
    {
        Assert.That(_layout.SidebarWidth, Is.EqualTo(AppSettings.Default.SidebarWidth));
        Assert.That(_layout.ResponseWidth, Is.EqualTo(AppSettings.Default.ResponseWidth));
        Assert.That(_layout.IsSidebarVisible, Is.True);
        Assert.That(_layout.IsResponseVisible, Is.True);
    }

    [Test]
    public void ApplyTakesTheStoredWidths()
    {
        _layout.Apply(AppSettings.Default with { SidebarWidth = 310, ResponseWidth = 520 });

        Assert.That(_layout.SidebarWidth, Is.EqualTo(310));
        Assert.That(_layout.ResponseWidth, Is.EqualTo(520));
    }

    [Test]
    public async Task RememberingAWidthStoresItAndKeepsTheOtherSettings()
    {
        var result = await _layout.RememberSidebarWidthAsync(333.4);
        await _layout.RememberResponseWidthAsync(555);

        var stored = (await _settings.GetAsync(CancellationToken.None)).Value!;
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(stored.SidebarWidth, Is.EqualTo(333));
        Assert.That(stored.ResponseWidth, Is.EqualTo(555));
        Assert.That(stored.TimeoutSeconds, Is.EqualTo(12));
        Assert.That(stored.OpenOnSingleClick, Is.True);
        Assert.That(_layout.SidebarWidth, Is.EqualTo(333));
    }

    [TestCase(5, 100)]
    [TestCase(99, 100)]
    [TestCase(250, 250)]
    [TestCase(9000, 4000)]
    public async Task RememberedWidthStaysInTheRangeTheSettingsAccept(double width, double expected)
    {
        var result = await _layout.RememberResponseWidthAsync(width);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_layout.ResponseWidth, Is.EqualTo(expected));
    }

    [Test]
    public async Task FailureToReadTheSettingsIsReturned()
    {
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<AppSettings>.Failure(new Error(ErrorCategory.Storage, "A setting could not be read."))));

        var result = await _layout.RememberSidebarWidthAsync(300);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Message, Is.EqualTo("A setting could not be read."));
        A.CallTo(() => _settings.SaveAsync(A<AppSettings>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public void StartsWithTheTopBarShown()
    {
        Assert.That(_layout.IsTopBarVisible, Is.True);
    }

    [Test]
    public void ApplyTakesWhatWasShown()
    {
        _layout.Apply(AppSettings.Default with { SidebarVisible = false, ResponseVisible = true, TopBarVisible = false });

        Assert.That(_layout.IsSidebarVisible, Is.False);
        Assert.That(_layout.IsResponseVisible, Is.True);
        Assert.That(_layout.IsTopBarVisible, Is.False);
    }

    // The Settings dialog applies the stored settings again. The View menu state, stored only at exit, must stay.
    [Test]
    public void ApplyingAgainKeepsWhatTheUserHasToggledSinceTheStart()
    {
        _layout.Apply(AppSettings.Default);
        _layout.IsTopBarVisible = false;
        _layout.IsSidebarVisible = false;

        _layout.Apply(AppSettings.Default with { SidebarWidth = 280 });

        Assert.That(_layout.IsTopBarVisible, Is.False);
        Assert.That(_layout.IsSidebarVisible, Is.False);
        Assert.That(_layout.SidebarWidth, Is.EqualTo(280));
    }

    [Test]
    public async Task RememberingVisibilityStoresItAndKeepsTheOtherSettings()
    {
        _layout.IsSidebarVisible = false;
        _layout.IsResponseVisible = false;
        _layout.IsTopBarVisible = false;

        var result = await _layout.RememberVisibilityAsync();

        var stored = (await _settings.GetAsync(CancellationToken.None)).Value!;
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(stored.SidebarVisible, Is.False);
        Assert.That(stored.ResponseVisible, Is.False);
        Assert.That(stored.TopBarVisible, Is.False);
        Assert.That(stored.TimeoutSeconds, Is.EqualTo(12));
    }

    [Test]
    public async Task HidingAPanelKeepsItsWidth()
    {
        await _layout.RememberSidebarWidthAsync(300);

        _layout.IsSidebarVisible = false;
        _layout.IsSidebarVisible = true;

        Assert.That(_layout.SidebarWidth, Is.EqualTo(300));
    }
}
