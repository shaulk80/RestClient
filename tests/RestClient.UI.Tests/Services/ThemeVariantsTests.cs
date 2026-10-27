using Avalonia.Styling;
using NUnit.Framework;
using RestClient.Application.Settings;
using RestClient.UI.Services;

namespace RestClient.UI.Tests.Services;

[TestFixture]
public class ThemeVariantsTests
{
    [Test]
    public void SystemFollowsTheOperatingSystem()
    {
        Assert.That(ThemeVariants.For(ThemePreference.System), Is.EqualTo(ThemeVariant.Default));
    }

    [Test]
    public void LightGivesTheLightVariant()
    {
        Assert.That(ThemeVariants.For(ThemePreference.Light), Is.EqualTo(ThemeVariant.Light));
    }

    [Test]
    public void DarkGivesTheDarkVariant()
    {
        Assert.That(ThemeVariants.For(ThemePreference.Dark), Is.EqualTo(ThemeVariant.Dark));
    }
}
