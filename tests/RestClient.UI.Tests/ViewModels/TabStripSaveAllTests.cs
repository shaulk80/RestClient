using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class TabStripSaveAllTests
{
    private IRequestService _requests = null!;
    private IDestinationPicker _destinations = null!;
    private AutosaveScheduler _autosave = null!;
    private TabStripViewModel _strip = null!;

    [SetUp]
    public void SetUp()
    {
        _requests = A.Fake<IRequestService>();
        _destinations = A.Fake<IDestinationPicker>();
        A.CallTo(() => _requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        A.CallTo(() => _requests.AddCopyAsync(A<Guid>._, A<RequestDefinition>._, A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult(Result<Guid>.Success(Guid.NewGuid())));
        _autosave = new AutosaveScheduler(TabStripFactory.NeverDelay);
        var services = RequestTabServicesFactory.Create(A.Fake<IRequestSender>(), _requests, _destinations);
        _strip = TabStripFactory.Create(services, autosave: _autosave);
    }

    [TearDown]
    public void TearDown()
    {
        _autosave.Dispose();
    }

    [Test]
    public async Task SavesEveryChangedLinkedTabAndSkipsCleanOnes()
    {
        var changedOne = await OpenLinkedAsync("One");
        var clean = await OpenLinkedAsync("Clean");
        var changedTwo = await OpenLinkedAsync("Two");
        changedOne.Url = "https://example.com/one";
        changedTwo.Url = "https://example.com/two";

        await _strip.SaveAllAsync();

        Assert.That(_strip.Tabs.Select(tab => tab.IsDirty), Is.All.False);
        A.CallTo(() => _requests.UpdateAsync(clean.RequestId, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    [Test]
    public async Task UnsavedTabsAskForAFolderAndANameOneAfterTheOther()
    {
        _strip.NewTab();
        _strip.NewTab();
        PickFolder();

        await _strip.SaveAllAsync();

        Assert.That(_strip.Tabs.Select(tab => tab.IsLinked), Is.All.True);
        A.CallTo(() => _destinations.PickAsync(A<string>._, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    }

    [Test]
    public async Task CancellingTheDialogStopsTheRestButTheLinkedTabsWereSaved()
    {
        var linked = await OpenLinkedAsync("Linked");
        linked.Url = "https://example.com/changed";
        _strip.NewTab();
        _strip.NewTab();
        A.CallTo(() => _destinations.PickAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<SaveDestination?>.Success(null)));

        await _strip.SaveAllAsync();

        Assert.That(linked.IsDirty, Is.False);
        Assert.That(_strip.Tabs.Count(tab => tab.IsLinked), Is.EqualTo(1));
        A.CallTo(() => _destinations.PickAsync(A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ATabThatFailsToSaveStaysChangedAndTheOthersAreStillSaved()
    {
        var failing = await OpenLinkedAsync("Failing");
        var fine = await OpenLinkedAsync("Fine");
        failing.Url = "https://example.com/failing";
        fine.Url = "https://example.com/fine";
        A.CallTo(() => _requests.UpdateAsync(failing.RequestId, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Storage, "The request could not be saved."))));

        await _strip.SaveAllAsync();

        Assert.That(failing.IsDirty, Is.True);
        Assert.That(failing.SaveError, Is.EqualTo("The request could not be saved."));
        Assert.That(fine.IsDirty, Is.False);
    }

    [Test]
    public async Task NothingChangedAsksNothing()
    {
        await OpenLinkedAsync("Clean");

        await _strip.SaveAllAsync();

        A.CallTo(() => _requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _destinations.PickAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private void PickFolder()
    {
        A.CallTo(() => _destinations.PickAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult(Result<SaveDestination?>.Success(
                new SaveDestination(Guid.NewGuid(), Guid.NewGuid(), "Saved"))));
    }

    private async Task<RequestTabViewModel> OpenLinkedAsync(string name)
    {
        var request = new RequestDefinition { Id = Guid.NewGuid(), Name = name, Url = "https://example.com" };
        A.CallTo(() => _requests.GetAsync(request.Id, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Success(request)));
        await _strip.OpenRequestAsync(request.Id);
        return _strip.Tabs[^1];
    }
}
