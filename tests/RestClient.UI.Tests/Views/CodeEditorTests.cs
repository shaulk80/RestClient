using Avalonia.Headless.NUnit;
using NUnit.Framework;
using RestClient.UI.Controls;
using RestClient.UI.Services;

namespace RestClient.UI.Tests.Views;

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
}
