namespace RestClient.UI.ViewModels;

public sealed class ConfirmationViewModel : ViewModelBase
{
    public ConfirmationViewModel(string message)
    {
        Message = message;
    }

    public string Message { get; }
}
