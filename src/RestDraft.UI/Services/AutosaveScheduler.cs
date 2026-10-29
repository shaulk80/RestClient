namespace RestDraft.UI.Services;

// Runs the latest scheduled action once changes stop for the delay. Every call must come from the UI thread.
public sealed class AutosaveScheduler : IDisposable
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(1);

    private readonly TimeSpan _delay;
    private CancellationTokenSource? _timer;
    private Func<Task>? _pending;

    public AutosaveScheduler(TimeSpan delay)
    {
        _delay = delay;
    }

    // Replaces any action that is still waiting and restarts the delay.
    public void Schedule(Func<Task> action)
    {
        _timer?.Cancel();
        _timer?.Dispose();
        _pending = action;
        _timer = new CancellationTokenSource();
        WaitThenRunAsync(_timer.Token);
    }

    // Runs the waiting action at once. Does nothing when none is waiting.
    public async Task FlushAsync()
    {
        _timer?.Cancel();
        await RunPendingAsync();
    }

    public void Dispose()
    {
        _timer?.Cancel();
        _timer?.Dispose();
    }

    private async void WaitThenRunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer change restarted the delay, or the action was flushed. The newer wait takes over.
            return;
        }

        await RunPendingAsync();
    }

    private async Task RunPendingAsync()
    {
        var action = _pending;
        _pending = null;
        if (action is not null)
        {
            await action();
        }
    }
}
