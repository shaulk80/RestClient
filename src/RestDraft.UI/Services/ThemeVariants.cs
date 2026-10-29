using Avalonia.Styling;
using RestDraft.Application.Settings;

namespace RestDraft.UI.Services;

public static class ThemeVariants
{
    // System is the default variant, which follows the operating system.
    public static ThemeVariant For(ThemePreference preference)
    {
        return preference switch
        {
            ThemePreference.Light => ThemeVariant.Light,
            ThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
