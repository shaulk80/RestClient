using Avalonia.Controls;

namespace RestDraft.UI.Views;

public partial class RequestDetailsView : UserControl
{
    public RequestDetailsView()
    {
        InitializeComponent();
    }

    // Puts the cursor in the body: the editor of a raw body, otherwise the body type box.
    public void FocusBody()
    {
        Body.FocusFirstInput();
    }
}
