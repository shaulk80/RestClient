using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using NUnit.Framework;
using RestDraft.UI.Controls;
using RestDraft.UI.Services;

namespace RestDraft.UI.Tests.Views;

[TestFixture]
public class CodeEditorTests
{
    [AvaloniaTest]
    public void EditorTextIsCopiedToTextAndBack()
    {
        var editor = new CodeEditor();

        editor.EditorText = "{}";
        Assert.That(editor.Text, Is.EqualTo("{}"));

        editor.Text = "[]";
        Assert.That(editor.EditorText, Is.EqualTo("[]"));
    }

    [AvaloniaTest]
    public void SwitchingLanguageAppliesAndRemovesHighlighting()
    {
        var editor = new CodeEditor();

        editor.Language = EditorLanguage.Json;
        editor.Language = EditorLanguage.Xml;
        editor.Language = EditorLanguage.PlainText;

        Assert.That(editor.Language, Is.EqualTo(EditorLanguage.PlainText));
    }

    [AvaloniaTest]
    public void SwitchingTheThemeKeepsTheTextAndLanguage()
    {
        var editor = new CodeEditor { EditorText = "{\"a\": 1}", Language = EditorLanguage.Json };
        var window = new Window { Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.RequestedThemeVariant = ThemeVariant.Dark;
        Dispatcher.UIThread.RunJobs();
        window.RequestedThemeVariant = ThemeVariant.Light;
        Dispatcher.UIThread.RunJobs();

        Assert.That(editor.Text, Is.EqualTo("{\"a\": 1}"));
        Assert.That(editor.Language, Is.EqualTo(EditorLanguage.Json));
        window.Close();
    }

    [TestCase(RawInputModifiers.None, true)]
    [TestCase(RawInputModifiers.Shift, false)]
    [AvaloniaTest]
    public void EscapeMovesFocusOutOfTheEditor(RawInputModifiers modifiers, bool forward)
    {
        var before = new TextBox();
        var editor = new CodeEditor();
        var after = new TextBox();
        var panel = new StackPanel();
        panel.Children.Add(before);
        panel.Children.Add(editor);
        panel.Children.Add(after);
        var window = new Window { Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        editor.TextArea.Focus();

        window.KeyPressQwerty(PhysicalKey.Escape, modifiers);
        Dispatcher.UIThread.RunJobs();

        Assert.That((forward ? after : before).IsFocused, Is.True);
        window.Close();
    }
}
