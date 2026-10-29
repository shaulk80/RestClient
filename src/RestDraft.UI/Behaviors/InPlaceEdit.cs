using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace RestDraft.UI.Behaviors;

// Makes a text box an in-place editor for a name. When the box becomes visible it takes the focus with its text
// selected, so typing starts at once. Leaving the box with the mouse or Tab runs the command, the same as Enter.
public static class InPlaceEdit
{
    public static readonly AttachedProperty<ICommand?> CommitOnLostFocusProperty =
        AvaloniaProperty.RegisterAttached<TextBox, TextBox, ICommand?>("CommitOnLostFocus");

    static InPlaceEdit()
    {
        CommitOnLostFocusProperty.Changed.AddClassHandler<TextBox>(OnCommandChanged);
    }

    public static ICommand? GetCommitOnLostFocus(TextBox box)
    {
        return box.GetValue(CommitOnLostFocusProperty);
    }

    public static void SetCommitOnLostFocus(TextBox box, ICommand? value)
    {
        box.SetValue(CommitOnLostFocusProperty, value);
    }

    // The property changes again when the box gets a new item, so the handlers are removed before they are added.
    private static void OnCommandChanged(TextBox box, AvaloniaPropertyChangedEventArgs e)
    {
        box.PropertyChanged -= OnPropertyChanged;
        box.LostFocus -= OnLostFocus;
        box.Loaded -= OnLoaded;
        if (e.NewValue is not ICommand)
        {
            return;
        }

        box.PropertyChanged += OnPropertyChanged;
        box.LostFocus += OnLostFocus;
        box.Loaded += OnLoaded;
    }

    private static void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is TextBox box && e.Property == Visual.IsVisibleProperty)
        {
            TakeFocus(box, e.NewValue is true);
        }
    }

    private static void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            TakeFocus(box, box.IsVisible);
        }
    }

    private static void OnLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox box && GetCommitOnLostFocus(box) is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    // Focus waits for the layout pass, because a control that has just appeared refuses it.
    private static void TakeFocus(TextBox box, bool isVisible)
    {
        if (!isVisible)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            box.Focus();
            box.SelectAll();
        }, DispatcherPriority.Background);
    }
}
