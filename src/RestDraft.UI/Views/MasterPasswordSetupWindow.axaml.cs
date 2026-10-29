using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RestDraft.UI.Views;

public partial class MasterPasswordSetupWindow : Window
{
    public MasterPasswordSetupWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
