namespace RestDraft.UI.Commands;

public sealed class AsyncRelayCommand : CommandBase
{
    private readonly Func<Task> _execute;

    public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute)
        : base(canExecute)
    {
        _execute = execute;
    }

    public override async void Execute(object? parameter)
    {
        await _execute();
    }
}
