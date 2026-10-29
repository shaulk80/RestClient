using RestDraft.Application.Secrets;
using RestDraft.UI.ViewModels;
using RestDraft.UI.Views;

namespace RestDraft.UI.Services;

public sealed class SecretDialogs : ISecretDialogs
{
    private readonly ISecretService _secrets;

    public SecretDialogs(ISecretService secrets)
    {
        _secrets = secrets;
    }

    public Task<bool> ShowUnlockAsync(CancellationToken cancellationToken)
    {
        var viewModel = new UnlockDialogViewModel(_secrets);
        var window = new UnlockDialogWindow { DataContext = viewModel };
        viewModel.Unlocked += (_, _) => window.Close(true);
        return ShowAsync(window);
    }

    public Task<bool> ShowSetUpAsync(CancellationToken cancellationToken)
    {
        var viewModel = new MasterPasswordSetupViewModel(_secrets);
        var window = new MasterPasswordSetupWindow { DataContext = viewModel };
        viewModel.Created += (_, _) => window.Close(true);
        return ShowAsync(window);
    }

    private static async Task<bool> ShowAsync(Avalonia.Controls.Window window)
    {
        if (MainWindowLocator.Find() is not { } owner)
        {
            return false;
        }

        return await window.ShowDialog<bool>(owner);
    }
}
