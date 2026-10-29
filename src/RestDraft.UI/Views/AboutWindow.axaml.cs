using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RestDraft.UI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {ReadVersion()}";
    }

    // The informational version can end with the build's commit after a plus sign. Users only need the number.
    private static string ReadVersion()
    {
        var assembly = typeof(AboutWindow).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
