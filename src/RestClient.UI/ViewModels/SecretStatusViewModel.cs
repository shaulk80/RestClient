using System.Windows.Input;
using RestClient.Application.Secrets;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

// The lock state shown in the status bar. The button locks when unlocked, and asks for the password otherwise.
public sealed class SecretStatusViewModel : ViewModelBase
{
    private readonly ISecretService _secrets;
    private readonly ISecretPrompt _prompt;
    private readonly AsyncRelayCommand _toggleCommand;
    private bool _isSetUp;
    private string _message = string.Empty;

    public SecretStatusViewModel(ISecretService secrets, ISecretPrompt prompt)
    {
        _secrets = secrets;
        _prompt = prompt;
        _toggleCommand = new AsyncRelayCommand(() => ToggleAsync(), () => true);
        _secrets.StateChanged += OnStateChanged;
    }

    public string Text
    {
        get
        {
            if (!_isSetUp)
            {
                return "Secrets: not set up";
            }

            return _secrets.IsUnlocked ? "Secrets: unlocked" : "Secrets: locked";
        }
    }

    public string ButtonText => _secrets.IsUnlocked ? "Lock" : "Unlock";

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public ICommand ToggleCommand => _toggleCommand;

    public async Task ReloadAsync()
    {
        var setUp = await _secrets.IsSetUpAsync(CancellationToken.None);
        if (!setUp.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(setUp.Error!);
            return;
        }

        _isSetUp = setUp.Value;
        RaiseStatus();
    }

    public async Task ToggleAsync()
    {
        if (_secrets.IsUnlocked)
        {
            _secrets.Lock();
            return;
        }

        var unlocked = await _prompt.EnsureUnlockedAsync(CancellationToken.None);
        if (!unlocked.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(unlocked.Error!);
            return;
        }

        Message = string.Empty;
        await ReloadAsync();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        RaiseStatus();
    }

    private void RaiseStatus()
    {
        RaisePropertyChanged(nameof(Text));
        RaisePropertyChanged(nameof(ButtonText));
    }
}
