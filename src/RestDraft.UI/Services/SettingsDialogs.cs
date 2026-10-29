using RestDraft.Application.Settings;
using RestDraft.UI.ViewModels;
using RestDraft.UI.Views;

namespace RestDraft.UI.Services;

public sealed class SettingsDialogs : ISettingsDialogs
{
    private readonly ISettingsService _settings;

    public SettingsDialogs(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task ShowAsync(CancellationToken cancellationToken)
    {
        if (MainWindowLocator.Find() is not { } owner)
        {
            return;
        }

        var viewModel = new SettingsViewModel(_settings);
        await viewModel.LoadAsync();
        var window = new SettingsWindow { DataContext = viewModel };
        await window.ShowDialog(owner);
    }
}
