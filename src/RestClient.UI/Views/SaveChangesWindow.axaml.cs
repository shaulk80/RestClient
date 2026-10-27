using Avalonia.Controls;
using Avalonia.Interactivity;
using RestClient.UI.Services;

namespace RestClient.UI.Views;

public partial class SaveChangesWindow : Window
{
    public SaveChangesWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(SaveChangesChoice.Cancel);
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        Close(SaveChangesChoice.Discard);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        Close(SaveChangesChoice.Save);
    }
}
