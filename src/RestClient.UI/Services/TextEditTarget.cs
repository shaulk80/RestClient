using Avalonia.Controls;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;

namespace RestClient.UI.Services;

// The text box or code editor that the Edit menu works on. Opening a menu moves the focus into the menu, so the
// window reports each focus change here, and the menu acts on the last text control that had the focus.
public sealed class TextEditTarget
{
    private TextBox? _box;
    private TextEditor? _editor;

    public void Remember(object? focused)
    {
        switch (focused)
        {
            case TextBox box:
                _box = box;
                _editor = null;
                break;
            case TextArea area when area.FindAncestorOfType<TextEditor>() is { } editor:
                _editor = editor;
                _box = null;
                break;
        }
    }

    public void Cut()
    {
        if (_box is { IsReadOnly: false } box && IsShown(box))
        {
            box.Cut();
        }

        if (_editor is { IsReadOnly: false } editor && IsShown(editor))
        {
            editor.Cut();
        }
    }

    public void Copy()
    {
        if (_box is { } box && IsShown(box))
        {
            box.Copy();
        }

        if (_editor is { } editor && IsShown(editor))
        {
            editor.Copy();
        }
    }

    public void Paste()
    {
        if (_box is { IsReadOnly: false } box && IsShown(box))
        {
            box.Paste();
        }

        if (_editor is { IsReadOnly: false } editor && IsShown(editor))
        {
            editor.Paste();
        }
    }

    public void SelectAll()
    {
        if (_box is { } box && IsShown(box))
        {
            box.SelectAll();
        }

        if (_editor is { } editor && IsShown(editor))
        {
            editor.SelectAll();
        }
    }

    // A control of a tab that was closed is detached, and must not be edited any more.
    private static bool IsShown(Avalonia.Visual control)
    {
        return TopLevel.GetTopLevel(control) is not null;
    }
}
