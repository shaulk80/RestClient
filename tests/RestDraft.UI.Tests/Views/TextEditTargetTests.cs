using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using NUnit.Framework;
using RestDraft.UI.Services;

namespace RestDraft.UI.Tests.Views;

// The Edit menu acts on the text control that had the focus before the menu took it.
[TestFixture]
public class TextEditTargetTests
{
    private Window? _window;

    [TearDown]
    public void TearDown()
    {
        _window?.Close();
    }

    [AvaloniaTest]
    public void SelectAllSelectsTheTextOfTheLastRememberedBox()
    {
        var first = new TextBox { Text = "first" };
        var second = new TextBox { Text = "second text" };
        Show(first, second);
        var target = new TextEditTarget();
        target.Remember(first);
        target.Remember(second);

        target.SelectAll();

        Assert.That(second.SelectedText, Is.EqualTo("second text"));
        Assert.That(first.SelectedText, Is.Empty);
    }

    [AvaloniaTest]
    public void ControlsThatAreNotTextBoxesAreIgnored()
    {
        var box = new TextBox { Text = "kept" };
        Show(box);
        var target = new TextEditTarget();
        target.Remember(box);

        target.Remember(new Button());
        target.SelectAll();

        Assert.That(box.SelectedText, Is.EqualTo("kept"));
    }

    [AvaloniaTest]
    public void ReadOnlyBoxIsNotCutOrPastedInto()
    {
        var box = new TextBox { Text = "fixed", IsReadOnly = true };
        Show(box);
        var target = new TextEditTarget();
        target.Remember(box);
        box.SelectAll();

        target.Cut();
        target.Paste();

        Assert.That(box.Text, Is.EqualTo("fixed"));
    }

    [AvaloniaTest]
    public void BoxThatLeftTheWindowIsNotTouched()
    {
        var box = new TextBox { Text = "gone" };
        Show(box);
        var target = new TextEditTarget();
        target.Remember(box);
        _window!.Content = null;
        Dispatcher.UIThread.RunJobs();

        target.SelectAll();

        Assert.That(box.SelectedText, Is.Empty);
    }

    [AvaloniaTest]
    public void NothingRememberedDoesNothing()
    {
        var target = new TextEditTarget();

        Assert.DoesNotThrow(() =>
        {
            target.Cut();
            target.Copy();
            target.Paste();
            target.SelectAll();
        });
    }

    private void Show(params TextBox[] boxes)
    {
        var panel = new StackPanel();
        foreach (var box in boxes)
        {
            panel.Children.Add(box);
        }

        _window = new Window { Content = panel };
        _window.Show();
        Dispatcher.UIThread.RunJobs();
    }
}
