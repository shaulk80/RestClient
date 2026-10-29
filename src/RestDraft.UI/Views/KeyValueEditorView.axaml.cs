using Avalonia;
using Avalonia.Controls;

namespace RestDraft.UI.Views;

public partial class KeyValueEditorView : UserControl
{
    public static readonly StyledProperty<bool> ShowAddButtonProperty =
        AvaloniaProperty.Register<KeyValueEditorView, bool>(nameof(ShowAddButton), defaultValue: true);

    public KeyValueEditorView()
    {
        InitializeComponent();
    }

    // The body tab puts the add button next to the body type, so it hides the one under the rows.
    public bool ShowAddButton
    {
        get => GetValue(ShowAddButtonProperty);
        set => SetValue(ShowAddButtonProperty, value);
    }
}
