using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace RestClient.UI.Services;

public static class MainWindowLocator
{
    public static Window? Find()
    {
        return (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}
