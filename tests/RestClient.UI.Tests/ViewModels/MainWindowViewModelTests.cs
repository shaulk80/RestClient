using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class MainWindowViewModelTests
{
    private TreeFixtures _fixtures = null!;
    private MainWindowViewModel _window = null!;

    [SetUp]
    public void SetUp()
    {
        _fixtures = new TreeFixtures();
        _window = new MainWindowViewModel(_fixtures.CreateSidebar(), A.Fake<IRequestSender>(), _fixtures.Requests);
        A.CallTo(() => _fixtures.Requests.GetAsync(_fixtures.Login.Id, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Success(_fixtures.Login)));
    }

    [Test]
    public async Task OpenRequestAddsTabAndSelectsIt()
    {
        await _window.OpenRequestAsync(_fixtures.Login.Id);

        Assert.That(_window.Tabs.Count, Is.EqualTo(1));
        Assert.That(_window.SelectedTab?.RequestId, Is.EqualTo(_fixtures.Login.Id));
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
    public async Task TreeReloadRenamesOpenTab()
    {
        await _window.OpenRequestAsync(_fixtures.Login.Id);
        var renamed = _fixtures.Login with { Name = "Sign in" };
        var users = _fixtures.Users with { Requests = [renamed] };
        _fixtures.SetCollections([new Collection(_fixtures.Shop.Id, _fixtures.Shop.Name, _fixtures.Root with { Folders = [users] })]);

        await _window.Sidebar.ReloadAsync();

        Assert.That(_window.Tabs[0].Name, Is.EqualTo("Sign in"));
    }
}
