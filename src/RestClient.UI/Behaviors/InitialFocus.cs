using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace RestClient.UI.Behaviors;

// Marks the control that takes keyboard focus when its window opens, so a dialog can be used without the mouse.
public static class InitialFocus
{
    public static readonly AttachedProperty<bool> IsTargetProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, bool>("IsTarget");

    static InitialFocus()
    {
        IsTargetProperty.Changed.AddClassHandler<Control>(OnIsTargetChanged);
    }

    public static bool GetIsTarget(Control control)
    {
        return control.GetValue(IsTargetProperty);
    }

    public static void SetIsTarget(Control control, bool value)
    {
        control.SetValue(IsTargetProperty, value);
    }

    private static void OnIsTargetChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.AttachedToVisualTree -= OnAttachedToVisualTree;
        if (e.NewValue is true)
        {
            control.AttachedToVisualTree += OnAttachedToVisualTree;
        }
    }

    // Focus waits for the first layout pass, because a control that is not yet measured refuses focus.
    private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => FocusControl(control), DispatcherPriority.Background);
    }

    // A list does not take the focus itself, only its items do. The selected item gets it, or the first one.
    private static void FocusControl(Control control)
    {
        if (control is SelectingItemsControl list)
        {
            list.ContainerFromIndex(Math.Max(list.SelectedIndex, 0))?.Focus();
            return;
        }

        control.Focus();
        if (control is TextBox box)
        {
            box.SelectAll();
        }
    }
}
