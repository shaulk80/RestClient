using NUnit.Framework;
using RestDraft.UI.Services;

namespace RestDraft.UI.Tests.Services;

[TestFixture]
public class AutosaveSchedulerTests
{
    private static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NeverDelay = TimeSpan.FromHours(1);
    private static readonly string[] ExpectedOrder = ["second"];

    [Test]
    public async Task ScheduledActionRunsAfterTheDelay()
    {
        using var scheduler = new AutosaveScheduler(ShortDelay);
        var ran = new TaskCompletionSource();

        scheduler.Schedule(() =>
        {
            ran.SetResult();
            return Task.CompletedTask;
        });

        Assert.That(await Task.WhenAny(ran.Task, Task.Delay(WaitLimit)), Is.SameAs(ran.Task));
    }

    [Test]
    public async Task ActionDoesNotRunBeforeTheDelayEnds()
    {
        using var scheduler = new AutosaveScheduler(NeverDelay);
        var runs = 0;

        scheduler.Schedule(() =>
        {
            runs++;
            return Task.CompletedTask;
        });
        await Task.Delay(ShortDelay * 3);

        Assert.That(runs, Is.EqualTo(0));
    }

    [Test]
    public async Task OnlyTheLatestScheduledActionRuns()
    {
        using var scheduler = new AutosaveScheduler(ShortDelay);
        var order = new List<string>();
        var ran = new TaskCompletionSource();

        scheduler.Schedule(() => Record(order, "first", null));
        scheduler.Schedule(() => Record(order, "second", ran));
        await Task.WhenAny(ran.Task, Task.Delay(WaitLimit));
        await Task.Delay(ShortDelay * 3);

        Assert.That(order, Is.EqualTo(ExpectedOrder));
    }

    [Test]
    public async Task FlushRunsTheWaitingActionAtOnceAndOnlyOnce()
    {
        using var scheduler = new AutosaveScheduler(ShortDelay);
        var runs = 0;
        scheduler.Schedule(() =>
        {
            runs++;
            return Task.CompletedTask;
        });

        await scheduler.FlushAsync();
        await Task.Delay(ShortDelay * 3);

        Assert.That(runs, Is.EqualTo(1));
    }

    [Test]
    public void FlushWithNothingScheduledDoesNothing()
    {
        using var scheduler = new AutosaveScheduler(ShortDelay);

        Assert.That(async () => await scheduler.FlushAsync(), Throws.Nothing);
    }

    private static Task Record(List<string> order, string name, TaskCompletionSource? ran)
    {
        order.Add(name);
        ran?.SetResult();
        return Task.CompletedTask;
    }
}
