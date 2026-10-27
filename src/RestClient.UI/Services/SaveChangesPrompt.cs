using RestClient.UI.ViewModels;
using RestClient.UI.Views;

namespace RestClient.UI.Services;

public sealed class SaveChangesPrompt : ISaveChangesPrompt
{
    public async Task<SaveChangesChoice> AskAsync(string requestName, CancellationToken cancellationToken)
    {
        if (MainWindowLocator.Find() is not { } owner)
        {
            return SaveChangesChoice.Cancel;
        }

        var window = new SaveChangesWindow { DataContext = new SaveChangesViewModel(requestName) };
        return await window.ShowDialog<SaveChangesChoice>(owner);
    }
}
