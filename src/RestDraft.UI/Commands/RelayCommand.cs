namespace RestDraft.UI.Commands;

public sealed class RelayCommand : CommandBase
{
    private readonly Action _execute;

    public RelayCommand(Action execute, Func<bool> canExecute)
        : base(canExecute)
    {
        _execute = execute;
    }

    public override void Execute(object? parameter)
    {
        _execute();
    }
}
