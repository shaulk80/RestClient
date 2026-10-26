using Avalonia.Controls;
using RestClient.UI.ViewModels;

namespace RestClient.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.Sidebar.ReloadAsync();
            await viewModel.History.ReloadAsync();
            await viewModel.Environments.ReloadAsync();
            await viewModel.Secrets.ReloadAsync();
        }
    }

    // A cleared list also clears the selection. Only a real choice is saved as the active environment.
    private async void OnEnvironmentSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is EnvironmentChoice choice)
        {
            await viewModel.Environments.SelectAsync(choice);
        }
    }
}
