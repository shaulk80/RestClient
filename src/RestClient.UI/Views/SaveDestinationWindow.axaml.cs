using Avalonia.Controls;
using Avalonia.Interactivity;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Views;

public partial class SaveDestinationWindow : Window
{
    public SaveDestinationWindow()
    {
        InitializeComponent();
    }

    private SaveDestinationViewModel? ViewModel => DataContext as SaveDestinationViewModel;

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Close(ViewModel?.Destination);
    }
}
