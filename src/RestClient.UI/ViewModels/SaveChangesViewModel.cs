namespace RestClient.UI.ViewModels;

public sealed class SaveChangesViewModel : ViewModelBase
{
    public SaveChangesViewModel(string requestName)
    {
        Message = $"\"{requestName}\" has unsaved changes. Save them before closing?";
    }

    public string Message { get; }
}
