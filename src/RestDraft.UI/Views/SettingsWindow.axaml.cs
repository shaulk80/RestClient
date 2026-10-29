using Avalonia.Controls;
using Avalonia.Interactivity;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.Saved += OnSaved;
        }
    }

    private void OnSaved(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
