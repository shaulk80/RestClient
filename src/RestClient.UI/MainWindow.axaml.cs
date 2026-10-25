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
        }
    }
}
