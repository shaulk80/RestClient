using Avalonia.Controls;

namespace RestClient.UI.Views;

public partial class BodyEditorView : UserControl
{
    public BodyEditorView()
    {
        InitializeComponent();
    }

    public void FocusFirstInput()
    {
        if (BodyCodeEditor.IsEffectivelyVisible)
        {
            BodyCodeEditor.TextArea.Focus();
            return;
        }

        BodyTypeBox.Focus();
    }
}
