using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using RestDraft.UI.Tests.Headless;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace RestDraft.UI.Tests.Headless;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<TestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
