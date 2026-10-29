using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

namespace RestDraft.UI.Tests.Headless;

public sealed class TestApplication : Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://RestDraft.UI.Tests/"))
        {
            Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml"),
        });
        Styles.Add(new StyleInclude(new Uri("avares://RestDraft.UI.Tests/"))
        {
            Source = new Uri("avares://RestDraft/Styles/AppStyles.axaml"),
        });
    }
}
