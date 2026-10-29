using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace RestDraft.UI.Tests.Views;

// Finds interactive controls that a screen reader would announce without a name. A control is named by an explicit
// automation name, or by text content it shows itself. Parts of control templates are skipped: the owning control
// carries the name.
internal static class AccessibleNameChecker
{
    public static IReadOnlyList<string> FindUnnamed(Visual root)
    {
        return root.GetVisualDescendants()
            .OfType<Control>()
            .Where(IsInteractive)
            .Where(control => control.TemplatedParent is null)
            .Where(control => !HasName(control))
            .Select(Describe)
            .ToList();
    }

    private static bool IsInteractive(Control control)
    {
        return control is TextBox or ComboBox or CheckBox or ListBox or TreeView or ItemsControl { Name: not null } or Button or RadioButton
            || control.GetType().Name == "CodeEditor";
    }

    private static bool HasName(Control control)
    {
        if (!string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)))
        {
            return true;
        }

        return control is ContentControl { Content: string { Length: > 0 } };
    }

    private static string Describe(Control control)
    {
        return $"{control.GetType().Name} (placeholder or content: {Hint(control)})";
    }

    private static string Hint(Control control)
    {
        return control switch
        {
            TextBox box => box.PlaceholderText ?? box.Text ?? string.Empty,
            ContentControl content => content.Content?.ToString() ?? string.Empty,
            _ => string.Empty,
        };
    }
}
