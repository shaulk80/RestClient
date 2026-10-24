using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using RestClient.UI.Tests.Headless;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace RestClient.UI.Tests.Headless;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<TestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
