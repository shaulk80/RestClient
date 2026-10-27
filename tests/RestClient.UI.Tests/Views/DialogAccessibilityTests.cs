using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using RestClient.UI.Behaviors;
using RestClient.UI.Views;

namespace RestClient.UI.Tests.Views;

[TestFixture]
public class DialogAccessibilityTests
{
    private Window? _window;

    [TearDown]
    public void TearDown()
    {
        _window?.Close();
    }

    [AvaloniaTest]
    public void CheckerReportsControlsWithoutAName()
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBox { PlaceholderText = "Unnamed" });
        panel.Children.Add(new TextBox { PlaceholderText = "Named" });
        AutomationProperties.SetName(panel.Children[1], "Named box");
        panel.Children.Add(new Button { Content = "Send" });
        panel.Children.Add(new Button());
        Show(new Window { Content = panel });

        var unnamed = AccessibleNameChecker.FindUnnamed(_window!);

        Assert.That(unnamed, Has.Count.EqualTo(2));
    }

    [AvaloniaTest]
    public void SettingsDialogControlsAreNamed()
    {
        Show(new SettingsWindow());

        Assert.That(AccessibleNameChecker.FindUnnamed(_window!), Is.Empty);
    }

    [AvaloniaTest]
    public void AboutDialogControlsAreNamed()
    {
        Show(new AboutWindow());

        Assert.That(AccessibleNameChecker.FindUnnamed(_window!), Is.Empty);
    }

    [AvaloniaTest]
    public void SaveDestinationDialogControlsAreNamed()
    {
        Show(new SaveDestinationWindow());

        Assert.That(AccessibleNameChecker.FindUnnamed(_window!), Is.Empty);
    }

    [AvaloniaTest]
    public void UnlockDialogControlsAreNamed()
    {
        Show(new UnlockDialogWindow());

        Assert.That(AccessibleNameChecker.FindUnnamed(_window!), Is.Empty);
    }

    [AvaloniaTest]
    public void MasterPasswordDialogControlsAreNamed()
    {
        Show(new MasterPasswordSetupWindow());

        Assert.That(AccessibleNameChecker.FindUnnamed(_window!), Is.Empty);
    }

    [AvaloniaTest]
    public void EnvironmentEditorControlsAreNamed()
    {
        Show(new EnvironmentEditorWindow());

        Assert.That(AccessibleNameChecker.FindUnnamed(_window!), Is.Empty);
    }

    [AvaloniaTest]
    public void ConfirmationDialogControlsAreNamed()
    {
        Show(new ConfirmationWindow());

        Assert.That(AccessibleNameChecker.FindUnnamed(_window!), Is.Empty);
    }

    [AvaloniaTest]
    public void SaveChangesDialogControlsAreNamed()
    {
        Show(new SaveChangesWindow());

        Assert.That(AccessibleNameChecker.FindUnnamed(_window!), Is.Empty);
    }

    [AvaloniaTest]
    public void InitialFocusTargetTakesFocusWhenTheWindowOpens()
    {
        var first = new TextBox();
        var target = new TextBox();
        InitialFocus.SetIsTarget(target, true);
        var panel = new StackPanel();
        panel.Children.Add(first);
        panel.Children.Add(target);

        Show(new Window { Content = panel });

        Assert.That(target.IsFocused, Is.True);
        Assert.That(first.IsFocused, Is.False);
    }

    [AvaloniaTest]
    public void SettingsDialogOpensWithTheTimeoutBoxFocused()
    {
        Show(new SettingsWindow());

        Assert.That(_window!.FocusManager?.GetFocusedElement(), Is.TypeOf<TextBox>());
    }

    private void Show(Window window)
    {
        _window = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
    }
}
