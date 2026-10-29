using RestDraft.UI.ViewModels;
using RestDraft.UI.Views;

namespace RestDraft.UI.Services;

public sealed class EnvironmentDialogs : IEnvironmentDialogs
{
    private readonly EnvironmentEditorViewModel _editor;

    public EnvironmentDialogs(EnvironmentEditorViewModel editor)
    {
        _editor = editor;
    }

    public async Task ShowEditorAsync(CancellationToken cancellationToken)
    {
        if (MainWindowLocator.Find() is not { } owner)
        {
            return;
        }

        await _editor.ReloadAsync();
        var window = new EnvironmentEditorWindow { DataContext = _editor };
        await window.ShowDialog(owner);
    }
}
