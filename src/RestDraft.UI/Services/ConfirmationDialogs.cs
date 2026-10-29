using RestDraft.UI.ViewModels;
using RestDraft.UI.Views;

namespace RestDraft.UI.Services;

public sealed class ConfirmationDialogs : IConfirmationDialogs
{
    public Task<bool> ConfirmAsync(string message, CancellationToken cancellationToken)
    {
        if (MainWindowLocator.Find() is not { } owner)
        {
            return Task.FromResult(false);
        }

        var window = new ConfirmationWindow { DataContext = new ConfirmationViewModel(message) };
        return window.ShowDialog<bool>(owner);
    }
}
