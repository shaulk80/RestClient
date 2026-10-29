using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Collections;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

// Ctrl+Tab goes through the open tabs in the order they were last used, not in the order of the strip.
[TestFixture]
public class TabStripRecentOrderTests
{
    private IRequestService _requests = null!;
    private AutosaveScheduler _autosave = null!;
    private TabStripViewModel _strip = null!;

    [SetUp]
    public void SetUp()
    {
        _requests = A.Fake<IRequestService>();
        _autosave = new AutosaveScheduler(TabStripFactory.NeverDelay);
        var services = RequestTabServicesFactory.Create(A.Fake<IRequestSender>(), _requests, A.Fake<IDestinationPicker>());
        _strip = TabStripFactory.Create(services, autosave: _autosave);
    }

    [TearDown]
    public void TearDown()
    {
        _autosave.Dispose();
    }

    [Test]
    public async Task SwitchGoesToThePreviouslyUsedTabNotTheNeighbour()
    {
        var tabs = await OpenTabsAsync("A", "B", "C");
        _strip.SelectedTab = tabs[0];

        _strip.SwitchRecent(forward: true);

        Assert.That(_strip.SelectedTab, Is.SameAs(tabs[2]));
    }

    [TestCase(1, "B")]
    [TestCase(2, "A")]
    [TestCase(3, "C")]
    [TestCase(-1, "A")]
    [TestCase(-2, "B")]
    public async Task HeldSwitchWalksBackThroughTheUsedOrderAndWraps(int steps, string expectedName)
    {
        await OpenTabsAsync("A", "B", "C");

        for (var step = 0; step < Math.Abs(steps); step++)
        {
            _strip.SwitchRecent(forward: steps > 0);
        }

        Assert.That(_strip.SelectedTab?.Name, Is.EqualTo(expectedName));
    }

    [Test]
    public async Task CommitMakesTheTabWhereTheRunStoppedTheMostRecentOne()
    {
        var tabs = await OpenTabsAsync("A", "B", "C");
        _strip.SwitchRecent(forward: true);
        _strip.SwitchRecent(forward: true);
        _strip.CommitRecentSwitch();

        _strip.SwitchRecent(forward: true);

        Assert.That(_strip.SelectedTab, Is.SameAs(tabs[2]));
    }

    [Test]
    public async Task SwitchWithoutCommitLeavesTheOrderAsItWas()
    {
        var tabs = await OpenTabsAsync("A", "B", "C");
        _strip.SwitchRecent(forward: true);
        _strip.SwitchRecent(forward: true);
        _strip.SwitchRecent(forward: false);
        _strip.CommitRecentSwitch();

        _strip.SwitchRecent(forward: true);

        Assert.That(_strip.SelectedTab, Is.SameAs(tabs[2]));
    }

    [Test]
    public async Task ChoosingATabEndsTheRunAndMakesItTheMostRecentOne()
    {
        var tabs = await OpenTabsAsync("A", "B", "C");
        _strip.SwitchRecent(forward: true);
        _strip.SwitchRecent(forward: true);
        _strip.SelectedTab = tabs[1];

        _strip.SwitchRecent(forward: true);

        Assert.That(_strip.SelectedTab, Is.SameAs(tabs[2]));
    }

    [Test]
    public async Task TwoTabsSwitchBackAndForth()
    {
        var tabs = await OpenTabsAsync("A", "B");

        _strip.SwitchRecent(forward: true);
        _strip.CommitRecentSwitch();
        var afterFirst = _strip.SelectedTab;
        _strip.SwitchRecent(forward: true);
        _strip.CommitRecentSwitch();

        Assert.That(afterFirst, Is.SameAs(tabs[0]));
        Assert.That(_strip.SelectedTab, Is.SameAs(tabs[1]));
    }

    [Test]
    public async Task OneTabOrNoTabStaysWhereItIs()
    {
        _strip.SwitchRecent(forward: true);
        var tabs = await OpenTabsAsync("A");

        _strip.SwitchRecent(forward: true);

        Assert.That(_strip.SelectedTab, Is.SameAs(tabs[0]));
    }

    [Test]
    public async Task ClosedTabIsLeftOutOfTheOrder()
    {
        var tabs = await OpenTabsAsync("A", "B", "C");
        await _strip.CloseTabAsync(tabs[1]);

        _strip.SwitchRecent(forward: true);

        Assert.That(_strip.SelectedTab, Is.SameAs(tabs[0]));
    }

    [Test]
    public async Task MenuCommandsSwitchAndCommitAtOnce()
    {
        var tabs = await OpenTabsAsync("A", "B", "C");

        _strip.NextRecentTabCommand.Execute(null);
        var afterFirst = _strip.SelectedTab;
        _strip.NextRecentTabCommand.Execute(null);
        var afterSecond = _strip.SelectedTab;
        _strip.PreviousRecentTabCommand.Execute(null);

        Assert.That(afterFirst, Is.SameAs(tabs[1]));
        Assert.That(afterSecond, Is.SameAs(tabs[2]));
        Assert.That(_strip.SelectedTab, Is.SameAs(tabs[0]));
    }

    private async Task<IReadOnlyList<RequestTabViewModel>> OpenTabsAsync(params string[] names)
    {
        foreach (var name in names)
        {
            var request = new RequestDefinition { Id = Guid.NewGuid(), Name = name, Url = "https://example.com" };
            A.CallTo(() => _requests.GetAsync(request.Id, A<CancellationToken>._))
                .Returns(Task.FromResult(Result<RequestDefinition>.Success(request)));
            await _strip.OpenRequestAsync(request.Id);
        }

        return [.. _strip.Tabs];
    }
}
