using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Views;

public partial class UnlockDialogWindow : Window
{
    public UnlockDialogWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not UnlockDialogViewModel viewModel)
        {
            return;
        }

        await viewModel.UnlockAsync();
    }
}
