using Avalonia;
using Avalonia.Controls;

namespace RestDraft.UI.Views;

public partial class MultipartEditorView : UserControl
{
    public static readonly StyledProperty<bool> ShowAddButtonProperty =
        AvaloniaProperty.Register<MultipartEditorView, bool>(nameof(ShowAddButton), defaultValue: true);

    public MultipartEditorView()
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
