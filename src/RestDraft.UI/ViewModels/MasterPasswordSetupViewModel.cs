using System.Windows.Input;
using RestDraft.Application.Secrets;
using RestDraft.UI.Commands;
using RestDraft.UI.Services;

namespace RestDraft.UI.ViewModels;

public sealed class MasterPasswordSetupViewModel : ViewModelBase
{
    private readonly ISecretService _secrets;
    private readonly AsyncRelayCommand _createCommand;
    private string _password = string.Empty;
    private string _confirmation = string.Empty;
    private string _message = string.Empty;

    public MasterPasswordSetupViewModel(ISecretService secrets)
    {
        _secrets = secrets;
        _createCommand = new AsyncRelayCommand(() => CreateAsync(), () => Password.Length > 0);
    }

    // Raised after the master password is set. The dialog closes on it.
    public event EventHandler? Created;

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                _createCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Confirmation
    {
        get => _confirmation;
        set => SetProperty(ref _confirmation, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public ICommand CreateCommand => _createCommand;

    public async Task<bool> CreateAsync()
    {
        if (Password != Confirmation)
        {
            Message = "The passwords do not match.";
            return false;
        }

        var result = await _secrets.SetUpAsync(Password, CancellationToken.None);
        if (!result.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(result.Error!);
            return false;
        }

        Password = string.Empty;
        Confirmation = string.Empty;
        Message = string.Empty;
        Created?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
