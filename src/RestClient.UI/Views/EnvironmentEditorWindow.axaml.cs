using Avalonia.Controls;
using Avalonia.Input;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Views;

public partial class EnvironmentEditorWindow : Window
{
    public EnvironmentEditorWindow()
    {
        InitializeComponent();
        List.AddHandler(KeyDownEvent, OnListKeyDown);
    }

    private EnvironmentEditorViewModel? ViewModel => DataContext as EnvironmentEditorViewModel;

    // F2 renames and Delete deletes the selected environment. Both belong to the name box while it is open,
    // where Delete is an editing key.
    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox || ViewModel is not { } viewModel)
        {
            return;
        }

        if (e.Key == Key.F2 && viewModel.RenameCommand.CanExecute(null))
        {
            e.Handled = true;
            viewModel.RenameCommand.Execute(null);
        }

        if (e.Key == Key.Delete && viewModel.DeleteCommand.CanExecute(null))
        {
            e.Handled = true;
            viewModel.DeleteCommand.Execute(null);
        }
    }
}
