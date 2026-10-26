using System.Windows.Input;
using RestClient.Application.Secrets;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class UnlockDialogViewModel : ViewModelBase
{
    private readonly ISecretService _secrets;
    private readonly AsyncRelayCommand _unlockCommand;
    private string _password = string.Empty;
    private string _message = string.Empty;

    public UnlockDialogViewModel(ISecretService secrets)
    {
        _secrets = secrets;
        _unlockCommand = new AsyncRelayCommand(() => UnlockAsync(), () => Password.Length > 0);
    }

    // Raised after a correct password. The dialog closes on it.
    public event EventHandler? Unlocked;

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                _unlockCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public ICommand UnlockCommand => _unlockCommand;

    public async Task<bool> UnlockAsync()
    {
        var result = await _secrets.UnlockAsync(Password, CancellationToken.None);
        if (!result.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(result.Error!);
            return false;
        }

        Password = string.Empty;
        Message = string.Empty;
        Unlocked?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
