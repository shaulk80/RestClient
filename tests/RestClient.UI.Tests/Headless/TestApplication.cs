using Avalonia;
using Avalonia.Themes.Fluent;

namespace RestClient.UI.Tests.Headless;

public sealed class TestApplication : Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
