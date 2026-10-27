using RestClient.Application.Settings;
using RestClient.UI.ViewModels;
using RestClient.UI.Views;

namespace RestClient.UI.Services;

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
