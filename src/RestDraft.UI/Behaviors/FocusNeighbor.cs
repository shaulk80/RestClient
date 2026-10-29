using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace RestDraft.UI.Behaviors;

// Finds the control that comes before or after a given control in tab order, skipping the control's own content.
// The built-in focus move cannot be used from inside the code editor: the editor hands focus to its text area,
// so a move lands back where it started.
public static class FocusNeighbor
{
    public static InputElement? Find(Visual origin, bool forward)
    {
        if (TopLevel.GetTopLevel(origin) is not { } root)
        {
            return null;
        }

        var all = root.GetVisualDescendants().ToList();
        var originIndex = all.IndexOf(origin);
        if (originIndex < 0)
        {
            return null;
        }

        IEnumerable<Visual> candidates = forward
            ? all.Skip(originIndex + 1).Where(visual => !origin.IsVisualAncestorOf(visual))
            : all.Take(originIndex).Where(visual => !visual.IsVisualAncestorOf(origin)).Reverse();
        return candidates.OfType<InputElement>().FirstOrDefault(CanTakeFocus);
    }

    private static bool CanTakeFocus(InputElement element)
    {
        return element.Focusable
            && element.IsTabStop
            && element.IsEffectivelyVisible
            && element.IsEffectivelyEnabled
            && (element as Control)?.TemplatedParent is null;
    }
}
