using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.Application.History;
using RestClient.Domain.Collections;
using RestClient.Domain.History;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

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

        Assert.That(_window.Tabs.Count, Is.EqualTo(1));
        Assert.That(_window.SelectedTab?.RequestId, Is.EqualTo(_fixtures.Login.Id));
        Assert.That(_window.SelectedTab?.IsLinked, Is.True);
        Assert.That(_window.HasTabs, Is.True);
    }

    [Test]
    public async Task OpeningSameRequestTwiceSelectsExistingTab()
    {
        await _window.OpenRequestAsync(_fixtures.Login.Id);
        await _window.OpenRequestAsync(_fixtures.Login.Id);

        Assert.That(_window.Tabs.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task OpenShowsErrorWhenRequestCannotBeLoaded()
    {
        A.CallTo(() => _fixtures.Requests.GetAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Failure(new Error(ErrorCategory.NotFound, "The request was not found."))));

        await _window.OpenRequestAsync(Guid.NewGuid());

        Assert.That(_window.Tabs, Is.Empty);
        Assert.That(_window.Sidebar.StatusMessage, Is.EqualTo("The request was not found."));
    }

    [Test]
    public async Task TreeReloadClosesTabWhenRequestWasDeleted()
    {
        await _window.OpenRequestAsync(_fixtures.Login.Id);
        _fixtures.SetCollections([new Collection(_fixtures.Shop.Id, _fixtures.Shop.Name, _fixtures.Root with { Folders = [] })]);

        await _window.Sidebar.ReloadAsync();

        Assert.That(_window.Tabs, Is.Empty);
        Assert.That(_window.SelectedTab, Is.Null);
    }

    [Test]
    public async Task TreeReloadKeepsUnsavedTab()
    {
        _window.OpenUnsaved(CreateUnsavedDefinition());
        _fixtures.SetCollections([new Collection(_fixtures.Shop.Id, _fixtures.Shop.Name, _fixtures.Root with { Folders = [] })]);

        await _window.Sidebar.ReloadAsync();

        Assert.That(_window.Tabs.Count, Is.EqualTo(1));
    }

    [Test]
    public void OpenUnsavedAddsNewTabEvenWhenSameRequestIsAlreadyOpen()
    {
        _window.OpenUnsaved(_fixtures.Login);
        _window.OpenUnsaved(_fixtures.Login);

        Assert.That(_window.Tabs.Count, Is.EqualTo(2));
        Assert.That(_window.Tabs.All(tab => !tab.IsLinked), Is.True);
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

        Assert.That(_window.Tabs.Count, Is.EqualTo(1));
        Assert.That(_window.Tabs[0].IsLinked, Is.False);
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

        await window.Tabs[0].SaveAsync();

        Assert.That(window.Tabs.Count, Is.EqualTo(1));
        Assert.That(window.Tabs[0].IsLinked, Is.True);
        Assert.That(window.Tabs[0].RequestId, Is.EqualTo(savedId));
        A.CallTo(() => _fixtures.Collections.ListAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task SendFromTabRefreshesHistory()
    {
        var sender = A.Fake<IRequestSender>();
        A.CallTo(() => sender.SendAsync(A<RequestDefinition>._, A<IReadOnlyDictionary<string, string>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Failure(new Error(ErrorCategory.Network, "Not sent."))));
        var window = CreateWindow(sender);
        await window.OpenRequestAsync(_fixtures.Login.Id);

        await window.Tabs[0].SendAsync();

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

        Assert.That(_window.Tabs[0].Name, Is.EqualTo("Sign in"));
    }

    private MainWindowViewModel CreateWindow(IRequestSender sender)
    {
        var sidebar = _fixtures.CreateSidebar();
        var history = new HistoryListViewModel(_historyService);
        return new MainWindowViewModel(sidebar, history, RequestTabServicesFactory.Create(sender, _fixtures.Requests, _destinations));
    }

    private static RequestDefinition CreateUnsavedDefinition()
    {
        return new RequestDefinition { Id = Guid.NewGuid(), Name = "Restored", Url = "https://example.com/restored" };
    }
}
