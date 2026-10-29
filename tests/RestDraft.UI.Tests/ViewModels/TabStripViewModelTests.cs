using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Collections;
using RestDraft.Application.Tabs;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Responses;
using RestDraft.Domain.Results;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

[TestFixture]
public class TabStripViewModelTests
{
    private static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(20);
    private static readonly string[] OnlyFirst = ["First"];
    private static readonly string[] RenamedAndNew = ["Renamed", "New request"];
    private static readonly string[] CleanAndDirtyNames = ["Clean", "Dirty"];
    private static readonly bool[] CleanAndDirtyFlags = [false, true];

    private IRequestService _requests = null!;
    private IRequestSender _sender = null!;
    private IDestinationPicker _destinations = null!;
    private ITabService _tabService = null!;
    private AutosaveScheduler _autosave = null!;
    private RequestTabServices _services = null!;
    private IReadOnlyList<TabSnapshot>? _stored;

    [SetUp]
    public void SetUp()
    {
        _stored = null;
        _requests = A.Fake<IRequestService>();
        _sender = A.Fake<IRequestSender>();
        _destinations = A.Fake<IDestinationPicker>();
        _autosave = new AutosaveScheduler(TabStripFactory.NeverDelay);
        _services = RequestTabServicesFactory.Create(_sender, _requests, _destinations);
        _tabService = TabStripFactory.CreateTabService();
        A.CallTo(() => _tabService.SaveAsync(A<IReadOnlyList<TabSnapshot>>._, A<CancellationToken>._))
            .Invokes((IReadOnlyList<TabSnapshot> tabs, CancellationToken _) => _stored = tabs)
            .Returns(Task.FromResult(Result.Success()));
    }

    [TearDown]
    public void TearDown()
    {
        _autosave.Dispose();
    }

    [Test]
    public void NewTabOpensSelectedUnsavedTab()
    {
        var strip = CreateStrip();

        strip.NewTabCommand.Execute(null);

        Assert.That(strip.Tabs, Has.Count.EqualTo(1));
        Assert.That(strip.SelectedTab, Is.SameAs(strip.Tabs[0]));
        Assert.That(strip.SelectedTab?.IsLinked, Is.False);
        Assert.That(strip.SelectedTab?.IsDirty, Is.True);
        Assert.That(strip.SelectedTab?.Name, Is.EqualTo("New request"));
    }

    [Test]
    public async Task ClosingCleanTabRemovesItWithoutAsking()
    {
        var prompt = TabStripFactory.CreatePrompt(SaveChangesChoice.Cancel);
        var strip = CreateStrip(prompt: prompt);
        await OpenLinkedAsync(strip, "Login");

        var closed = await strip.CloseTabAsync(strip.Tabs[0]);

        Assert.That(closed, Is.True);
        Assert.That(strip.Tabs, Is.Empty);
        Assert.That(strip.SelectedTab, Is.Null);
        A.CallTo(() => prompt.AskAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [TestCase(SaveChangesChoice.Cancel, false, 1)]
    [TestCase(SaveChangesChoice.Discard, true, 0)]
    public async Task ClosingDirtyTabAsksAndFollowsTheAnswer(SaveChangesChoice choice, bool expectedClosed, int expectedTabs)
    {
        var prompt = TabStripFactory.CreatePrompt(choice);
        var strip = CreateStrip(prompt: prompt);
        strip.NewTab();

        var closed = await strip.CloseTabAsync(strip.Tabs[0]);

        Assert.That(closed, Is.EqualTo(expectedClosed));
        Assert.That(strip.Tabs, Has.Count.EqualTo(expectedTabs));
        A.CallTo(() => prompt.AskAsync("New request", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ClosingDirtyLinkedTabWithSaveSavesThenCloses()
    {
        A.CallTo(() => _requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        var strip = CreateStrip(prompt: TabStripFactory.CreatePrompt(SaveChangesChoice.Save));
        await OpenLinkedAsync(strip, "Login");
        strip.Tabs[0].Url = "https://example.com/changed";

        var closed = await strip.CloseTabAsync(strip.Tabs[0]);

        Assert.That(closed, Is.True);
        Assert.That(strip.Tabs, Is.Empty);
        A.CallTo(() => _requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ClosingUnsavedTabWithSaveKeepsItOpenWhenFolderPickerIsCancelled()
    {
        A.CallTo(() => _destinations.PickAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<SaveDestination?>.Success(null)));
        var strip = CreateStrip(prompt: TabStripFactory.CreatePrompt(SaveChangesChoice.Save));
        strip.NewTab();

        var closed = await strip.CloseTabAsync(strip.Tabs[0]);

        Assert.That(closed, Is.False);
        Assert.That(strip.Tabs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ClosingDirtyTabWithSaveKeepsItOpenWhenSaveFails()
    {
        A.CallTo(() => _requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Storage, "The request could not be saved."))));
        var strip = CreateStrip(prompt: TabStripFactory.CreatePrompt(SaveChangesChoice.Save));
        await OpenLinkedAsync(strip, "Login");
        strip.Tabs[0].Url = "https://example.com/changed";

        var closed = await strip.CloseTabAsync(strip.Tabs[0]);

        Assert.That(closed, Is.False);
        Assert.That(strip.Tabs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task TabCloseButtonClosesCleanTab()
    {
        var strip = CreateStrip();
        await OpenLinkedAsync(strip, "Login");

        strip.Tabs[0].CloseCommand.Execute(null);

        Assert.That(strip.Tabs, Is.Empty);
    }

    [Test]
    public async Task ClosingSelectedTabSelectsItsNeighbour()
    {
        var strip = CreateStrip();
        await OpenLinkedAsync(strip, "First");
        await OpenLinkedAsync(strip, "Second");
        await OpenLinkedAsync(strip, "Third");
        strip.SelectedTab = strip.Tabs[1];

        await strip.CloseTabAsync(strip.Tabs[1]);

        Assert.That(strip.SelectedTab?.Name, Is.EqualTo("Third"));
    }

    [Test]
    public async Task ClosingSelectedTabCommandClosesTheSelectedTab()
    {
        var strip = CreateStrip();
        await OpenLinkedAsync(strip, "First");
        await OpenLinkedAsync(strip, "Second");

        strip.CloseSelectedCommand.Execute(null);

        Assert.That(strip.Tabs.Select(tab => tab.Name), Is.EqualTo(OnlyFirst));
    }

    [Test]
    public async Task SaveSelectedCommandSavesTheSelectedTab()
    {
        A.CallTo(() => _requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        var strip = CreateStrip();
        await OpenLinkedAsync(strip, "Login");
        strip.Tabs[0].Url = "https://example.com/changed";

        strip.SaveSelectedCommand.Execute(null);

        Assert.That(strip.Tabs[0].IsDirty, Is.False);
    }

    [Test]
    public async Task SendSelectedCommandSendsTheSelectedTab()
    {
        A.CallTo(() => _sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Failure(new Error(ErrorCategory.Network, "Not sent."))));
        var strip = CreateStrip();
        await OpenLinkedAsync(strip, "Login");

        strip.SendSelectedCommand.Execute(null);

        A.CallTo(() => _sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public void ShortcutCommandsDoNothingWithoutTabs()
    {
        var strip = CreateStrip();

        strip.SendSelectedCommand.Execute(null);
        strip.SaveSelectedCommand.Execute(null);
        strip.CloseSelectedCommand.Execute(null);

        A.CallTo(() => _sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._)).MustNotHaveHappened();
        Assert.That(strip.Tabs, Is.Empty);
    }

    [Test]
    public async Task OpeningSameRequestTwiceSelectsExistingTab()
    {
        var strip = CreateStrip();
        var request = await OpenLinkedAsync(strip, "Login");
        strip.NewTab();

        await strip.OpenRequestAsync(request.Id);

        Assert.That(strip.Tabs, Has.Count.EqualTo(2));
        Assert.That(strip.SelectedTab, Is.SameAs(strip.Tabs[0]));
    }

    [Test]
    public async Task SyncWithTreeRenamesLinkedTabsAndClosesDeletedOnes()
    {
        var strip = CreateStrip();
        var kept = await OpenLinkedAsync(strip, "Kept");
        await OpenLinkedAsync(strip, "Deleted");
        strip.NewTab();

        strip.SyncWithTree(id => id == kept.Id ? "Renamed" : null);

        Assert.That(strip.Tabs.Select(tab => tab.Name), Is.EqualTo(RenamedAndNew));
    }

    [Test]
    public async Task RestoreOpensStoredTabsAndSelectsTheStoredOne()
    {
        var clean = Definition("Clean");
        var dirty = Definition("Dirty");
        A.CallTo(() => _tabService.RestoreAsync(A<CancellationToken>._)).Returns(Task.FromResult(
            Result<IReadOnlyList<RestoredTab>>.Success(
            [
                new RestoredTab(clean, IsLinked: true, IsDirty: false, IsSelected: false),
                new RestoredTab(dirty, IsLinked: true, IsDirty: true, IsSelected: true),
            ])));
        var strip = CreateStrip();

        await strip.RestoreAsync();

        Assert.That(strip.Tabs.Select(tab => tab.Name), Is.EqualTo(CleanAndDirtyNames));
        Assert.That(strip.Tabs.Select(tab => tab.IsDirty), Is.EqualTo(CleanAndDirtyFlags));
        Assert.That(strip.SelectedTab, Is.SameAs(strip.Tabs[1]));
    }

    [Test]
    public async Task RestoreSelectsLastTabWhenNoneIsMarkedSelected()
    {
        A.CallTo(() => _tabService.RestoreAsync(A<CancellationToken>._)).Returns(Task.FromResult(
            Result<IReadOnlyList<RestoredTab>>.Success(
            [
                new RestoredTab(Definition("First"), IsLinked: true, IsDirty: false, IsSelected: false),
                new RestoredTab(Definition("Second"), IsLinked: true, IsDirty: false, IsSelected: false),
            ])));
        var strip = CreateStrip();

        await strip.RestoreAsync();

        Assert.That(strip.SelectedTab?.Name, Is.EqualTo("Second"));
    }

    [Test]
    public async Task RestoreFailureShowsErrorAndOpensNothing()
    {
        A.CallTo(() => _tabService.RestoreAsync(A<CancellationToken>._)).Returns(Task.FromResult(
            Result<IReadOnlyList<RestoredTab>>.Failure(new Error(ErrorCategory.Storage, "The open tabs could not be loaded."))));
        var strip = CreateStrip();

        await strip.RestoreAsync();

        Assert.That(strip.Tabs, Is.Empty);
        Assert.That(strip.AutosaveError, Is.EqualTo("The open tabs could not be loaded."));
    }

    [Test]
    public async Task TabsAreNotStoredBeforeRestoreHasRun()
    {
        var strip = CreateStrip();

        strip.NewTab();
        await strip.FlushAsync();

        A.CallTo(() => _tabService.SaveAsync(A<IReadOnlyList<TabSnapshot>>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task EditsAreStoredWithTheCurrentContentAndSelection()
    {
        var strip = CreateStrip();
        await strip.RestoreAsync();
        await OpenLinkedAsync(strip, "First");
        await OpenLinkedAsync(strip, "Second");
        strip.Tabs[0].Url = "https://example.com/edited";

        await strip.FlushAsync();

        Assert.That(_stored, Has.Count.EqualTo(2));
        Assert.That(_stored![0].Current.Url, Is.EqualTo("https://example.com/edited"));
        Assert.That(_stored![0].IsDirty, Is.True);
        Assert.That(_stored![0].IsLinked, Is.True);
        Assert.That(_stored![0].IsSelected, Is.False);
        Assert.That(_stored![1].IsSelected, Is.True);
        Assert.That(_stored![1].IsDirty, Is.False);
    }

    [Test]
    public async Task ChangesAreStoredAfterTheDelayWithoutFlushing()
    {
        using var autosave = new AutosaveScheduler(ShortDelay);
        var stored = new TaskCompletionSource();
        A.CallTo(() => _tabService.SaveAsync(A<IReadOnlyList<TabSnapshot>>._, A<CancellationToken>._))
            .Invokes(() => stored.TrySetResult())
            .Returns(Task.FromResult(Result.Success()));
        var strip = CreateStrip(autosave: autosave);
        await strip.RestoreAsync();

        strip.NewTab();

        Assert.That(await Task.WhenAny(stored.Task, Task.Delay(TimeSpan.FromSeconds(5))), Is.SameAs(stored.Task));
    }

    [Test]
    public async Task ClosingTheLastTabStoresAnEmptyList()
    {
        var strip = CreateStrip();
        await strip.RestoreAsync();
        await OpenLinkedAsync(strip, "Login");
        await strip.CloseTabAsync(strip.Tabs[0]);

        await strip.FlushAsync();

        Assert.That(_stored, Is.Empty);
    }

    [Test]
    public async Task StoreFailureIsShownAndClearedByTheNextSuccess()
    {
        var results = new Queue<Result>([Result.Failure(new Error(ErrorCategory.Storage, "The open tabs could not be saved.")), Result.Success()]);
        A.CallTo(() => _tabService.SaveAsync(A<IReadOnlyList<TabSnapshot>>._, A<CancellationToken>._))
            .ReturnsLazily(() => Task.FromResult(results.Dequeue()));
        var strip = CreateStrip();
        await strip.RestoreAsync();

        strip.NewTab();
        await strip.FlushAsync();
        var shown = strip.AutosaveError;
        strip.NewTab();
        await strip.FlushAsync();

        Assert.That(shown, Is.EqualTo("The open tabs could not be saved."));
        Assert.That(strip.AutosaveError, Is.Empty);
    }

    [Test]
    public async Task SendingARequestRaisesRequestSent()
    {
        A.CallTo(() => _sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Failure(new Error(ErrorCategory.Network, "Not sent."))));
        var strip = CreateStrip();
        await OpenLinkedAsync(strip, "Login");
        var raised = 0;
        strip.RequestSent += (_, _) => raised++;

        await strip.Tabs[0].SendAsync();

        Assert.That(raised, Is.EqualTo(1));
    }

    private TabStripViewModel CreateStrip(ISaveChangesPrompt? prompt = null, AutosaveScheduler? autosave = null)
    {
        return TabStripFactory.Create(_services, _tabService, prompt, autosave ?? _autosave);
    }

    private static RequestDefinition Definition(string name)
    {
        return new RequestDefinition { Id = Guid.NewGuid(), Name = name, Url = "https://example.com" };
    }

    private async Task<RequestDefinition> OpenLinkedAsync(TabStripViewModel strip, string name)
    {
        var request = Definition(name);
        A.CallTo(() => _requests.GetAsync(request.Id, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Success(request)));
        await strip.OpenRequestAsync(request.Id);
        return request;
    }
}
