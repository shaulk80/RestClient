using RestDraft.Application.Settings;

namespace RestDraft.UI.ViewModels;

public sealed record ThemeChoice(ThemePreference Theme, string Label)
{
    // The combo box reads its value for screen readers from the text of the item.
    public override string ToString()
    {
        return Label;
    }
}
