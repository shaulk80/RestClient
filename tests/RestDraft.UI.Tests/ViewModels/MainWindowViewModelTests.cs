using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Collections;
using RestDraft.Application.Environments;
using RestDraft.Application.History;
using RestDraft.Application.Secrets;
using RestDraft.Application.Settings;
using RestDraft.Domain.Collections;
using RestDraft.Domain.History;
using RestDraft.Domain.Responses;
using RestDraft.Domain.Results;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

[TestFixture]
public class MainWindowViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private TreeFixtures _fixtures = null!;
    private MainWindowViewModel _window = null!;
    private IHistoryService _historyService = null!;
    private IDestinationPicker _destinations = null!;

    [SetUp]
    public void SetUp()
    {
        _fixtures = new TreeFixtures();
        _historyService = A.Fake<IHistoryService>();
        _destinations = A.Fake<IDestinationPicker>();
        _window = CreateWindow(A.Fake<IRequestSender>());
        A.CallTo(() => _fixtures.Requests.GetAsync(_fixtures.Login.Id, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Success(_fixtures.Login)));
    }

    [Test]
    public async Task OpenRequestAddsTabAndSelectsIt()
    {
        await _window.OpenRequestAsync(_fixtures.Login.Id);

        Assert.That(_window.TabStrip.Tabs.Count, Is.EqualTo(1));
        Assert.That(_window.TabStrip.SelectedTab?.RequestId, Is.EqualTo(_fixtures.Login.Id));
        Assert.That(_window.TabStrip.SelectedTab?.IsLinked, Is.True);
        Assert.That(_window.TabStrip.HasTabs, Is.True);
    }

    [Test]
    public async Task OpeningSameRequestTwiceSelectsExistingTab()
    {
        await _window.OpenRequestAsync(_fixtures.Login.Id);
        await _window.OpenRequestAsync(_fixtures.Login.Id);

        Assert.That(_window.TabStrip.Tabs.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task OpenShowsErrorWhenRequestCannotBeLoaded()
    {
        A.CallTo(() => _fixtures.Requests.GetAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Failure(new Error(ErrorCategory.NotFound, "The request was not found."))));

        await _window.OpenRequestAsync(Guid.NewGuid());

        Assert.That(_window.TabStrip.Tabs, Is.Empty);
        Assert.That(_window.Sidebar.StatusMessage, Is.EqualTo("The request was not found."));
    }

    [Test]
    public async Task TreeReloadClosesTabWhenRequestWasDeleted()
    {
        await _window.OpenRequestAsync(_fixtures.Login.Id);
        _fixtures.SetCollections([new Collection(_fixtures.Shop.Id, _fixtures.Shop.Name, _fixtures.Root with { Folders = [] })]);

        await _window.Sidebar.ReloadAsync();

        Assert.That(_window.TabStrip.Tabs, Is.Empty);
        Assert.That(_window.TabStrip.SelectedTab, Is.Null);
    }

    [Test]
    public async Task TreeReloadKeepsUnsavedTab()
    {
        _window.OpenUnsaved(CreateUnsavedDefinition());
        _fixtures.SetCollections([new Collection(_fixtures.Shop.Id, _fixtures.Shop.Name, _fixtures.Root with { Folders = [] })]);

        await _window.Sidebar.ReloadAsync();

        Assert.That(_window.TabStrip.Tabs.Count, Is.EqualTo(1));
    }

    [Test]
    public void OpenUnsavedAddsNewTabEvenWhenSameRequestIsAlreadyOpen()
    {
        _window.OpenUnsaved(_fixtures.Login);
        _window.OpenUnsaved(_fixtures.Login);

        Assert.That(_window.TabStrip.Tabs.Count, Is.EqualTo(2));
        Assert.That(_window.TabStrip.Tabs.All(tab => !tab.IsLinked), Is.True);
    }

    [Test]
    public async Task RestoreFromHistoryOpensUnsavedTabWithoutTouchingTheTree()
    {
        var entry = new HistoryEntry(Guid.NewGuid(), Now, RequestMethod.Get, _fixtures.Login.Url, _fixtures.Login, 200, 1, null, null);
        var restored = CreateUnsavedDefinition();
        A.CallTo(() => _historyService.SearchAsync(A<HistoryFilter>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<HistoryEntry>>.Success([entry])));
        A.CallTo(() => _historyService.RestoreAsync(entry.Id, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Success(restored)));
        await _window.History.ReloadAsync();
        _window.History.SelectedItem = _window.History.Items[0];

        await _window.History.RestoreAsync();

        Assert.That(_window.TabStrip.Tabs.Count, Is.EqualTo(1));
        Assert.That(_window.TabStrip.Tabs[0].IsLinked, Is.False);
        A.CallTo(() => _fixtures.Collections.ListAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task SavingUnsavedTabReloadsTreeSoTheNewRequestIsShown()
    {
        var savedId = Guid.NewGuid();
        var saved = new RequestDefinition { Id = savedId, Name = "Restored" };
        _fixtures.SetCollections([new Collection(_fixtures.Shop.Id, _fixtures.Shop.Name, _fixtures.Root with { Requests = [saved] })]);
        var folderId = _fixtures.Root.Id;
        A.CallTo(() => _destinations.PickAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<SaveDestination?>.Success(new SaveDestination(folderId, _fixtures.Shop.Id, "Restored"))));
        A.CallTo(() => _fixtures.Requests.AddCopyAsync(folderId, A<RequestDefinition>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<Guid>.Success(savedId)));
        var window = CreateWindow(A.Fake<IRequestSender>());
        window.OpenUnsaved(CreateUnsavedDefinition());

        await window.TabStrip.Tabs[0].SaveAsync();

        Assert.That(window.TabStrip.Tabs.Count, Is.EqualTo(1));
        Assert.That(window.TabStrip.Tabs[0].IsLinked, Is.True);
        Assert.That(window.TabStrip.Tabs[0].RequestId, Is.EqualTo(savedId));
        A.CallTo(() => _fixtures.Collections.ListAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SendFromTabRefreshesHistory()
    {
        var sender = A.Fake<IRequestSender>();
        A.CallTo(() => sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Failure(new Error(ErrorCategory.Network, "Not sent."))));
        var window = CreateWindow(sender);
        await window.OpenRequestAsync(_fixtures.Login.Id);

        await window.TabStrip.Tabs[0].SendAsync();

        A.CallTo(() => _historyService.SearchAsync(A<HistoryFilter>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task TreeReloadRenamesOpenTab()
    {
        await _window.OpenRequestAsync(_fixtures.Login.Id);
        var renamed = _fixtures.Login with { Name = "Sign in" };
        var users = _fixtures.Users with { Requests = [renamed] };
        _fixtures.SetCollections([new Collection(_fixtures.Shop.Id, _fixtures.Shop.Name, _fixtures.Root with { Folders = [users] })]);

        await _window.Sidebar.ReloadAsync();

        Assert.That(_window.TabStrip.Tabs[0].Name, Is.EqualTo("Sign in"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ApplySettingsGivesTheTreeTheClickChoiceAndTheLayoutTheWidths(bool opensOnSingleClick)
    {
        var settings = SettingsServiceFactory.Create(
            AppSettings.Default with { OpenOnSingleClick = opensOnSingleClick, SidebarWidth = 301, ResponseWidth = 502 });
        var window = CreateWindow(A.Fake<IRequestSender>(), settings);

        await window.ApplySettingsAsync();

        Assert.That(window.Sidebar.OpensOnSingleClick, Is.EqualTo(opensOnSingleClick));
        Assert.That(window.Layout.SidebarWidth, Is.EqualTo(301));
        Assert.That(window.Layout.ResponseWidth, Is.EqualTo(502));
    }

    [TestCase(ThemePreference.System)]
    [TestCase(ThemePreference.Light)]
    [TestCase(ThemePreference.Dark)]
    public async Task ApplySettingsTakesTheTheme(ThemePreference theme)
    {
        var settings = SettingsServiceFactory.Create(AppSettings.Default with { Theme = theme });
        var window = CreateWindow(A.Fake<IRequestSender>(), settings);

        await window.ApplySettingsAsync();

        Assert.That(window.Theme, Is.EqualTo(theme));
    }

    [Test]
    public async Task ApplySettingsReportsASettingsFailureInTheMessageLine()
    {
        var settings = A.Fake<ISettingsService>();
        A.CallTo(() => settings.GetAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<AppSettings>.Failure(new Error(ErrorCategory.Storage, "A setting could not be read."))));
        var window = CreateWindow(A.Fake<IRequestSender>(), settings);

        await window.ApplySettingsAsync();

        Assert.That(window.Sidebar.StatusMessage, Is.EqualTo("A setting could not be read."));
        Assert.That(window.Sidebar.OpensOnSingleClick, Is.False);
    }

    [Test]
    public async Task SettingsDialogChangesApplyAfterItCloses()
    {
        var settings = SettingsServiceFactory.Create();
        var dialogs = A.Fake<ISettingsDialogs>();
        A.CallTo(() => dialogs.ShowAsync(A<CancellationToken>._))
            .Invokes(() => settings.SaveAsync(AppSettings.Default with { OpenOnSingleClick = true }, CancellationToken.None))
            .Returns(Task.CompletedTask);
        var window = CreateWindow(A.Fake<IRequestSender>(), settings, dialogs);

        window.SettingsCommand.Execute(null);
        await Task.Delay(50);

        Assert.That(window.Sidebar.OpensOnSingleClick, Is.True);
    }

    [Test]
    public async Task FailureToStoreAWidthIsShownInTheMessageLine()
    {
        var settings = SettingsServiceFactory.Create();
        A.CallTo(() => settings.SaveAsync(A<AppSettings>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Storage, "The settings could not be saved."))));
        var window = CreateWindow(A.Fake<IRequestSender>(), settings);

        await window.RememberResponseWidthAsync(400);

        Assert.That(window.Sidebar.StatusMessage, Is.EqualTo("The settings could not be saved."));
    }

    private MainWindowViewModel CreateWindow(IRequestSender sender, ISettingsService? settings = null, ISettingsDialogs? dialogs = null)
    {
        var sidebar = _fixtures.CreateSidebar();
        var history = new HistoryListViewModel(_historyService);
        var environments = new EnvironmentSelectorViewModel(A.Fake<IEnvironmentService>(), A.Fake<IEnvironmentDialogs>());
        var secrets = new SecretStatusViewModel(A.Fake<ISecretService>(), A.Fake<ISecretPrompt>());
        var tabs = TabStripFactory.Create(RequestTabServicesFactory.Create(sender, _fixtures.Requests, _destinations));
        var settingsService = settings ?? SettingsServiceFactory.Create();
        return new MainWindowViewModel(
            sidebar,
            history,
            tabs,
            environments,
            secrets,
            new WorkspaceLayoutViewModel(settingsService),
            settingsService,
            dialogs ?? A.Fake<ISettingsDialogs>());
    }

    private static RequestDefinition CreateUnsavedDefinition()
    {
        return new RequestDefinition { Id = Guid.NewGuid(), Name = "Restored", Url = "https://example.com/restored" };
    }
}
