using System.Windows.Input;

namespace RestDraft.UI.Commands;

public abstract class CommandBase : ICommand
{
    private readonly Func<bool> _canExecute;

    protected CommandBase(Func<bool> canExecute)
    {
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute();
    }

    public abstract void Execute(object? parameter);

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
