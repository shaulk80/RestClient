using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using FakeItEasy;
using NUnit.Framework;
using RestClient.Domain.Bodies;
using RestClient.UI.Controls;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;
using RestClient.UI.Views;
using RestClient.UI.Tests.ViewModels;

namespace RestClient.UI.Tests.Views;

[TestFixture]
public class BodyEditorViewLayoutTests
{
    [AvaloniaTest]
    public void RawBodyShowsEditorWithHeight()
    {
        var window = ShowEditor(new RawBody("text/plain", "hello"));

        var editor = window.GetVisualDescendants().OfType<CodeEditor>().Single();

        Assert.That(editor.IsEffectivelyVisible, Is.True);
        Assert.That(editor.TextArea.Bounds.Height, Is.GreaterThan(0));
    }

    [AvaloniaTest]
    public void NoBodyStillShowsEditorSectionHint()
    {
        var window = ShowEditor(new NoBody());

        var hint = window.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "NoBodyHint");

        Assert.That(hint.IsEffectivelyVisible, Is.True);
    }

    private static Window ShowEditor(RestClient.Domain.Bodies.BodyDefinition body)
    {
        var viewModel = new BodyEditorViewModel(RequestTabServicesFactory.CreateFormatting(), A.Fake<IFilePickerService>(), body);
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new BodyEditorView { DataContext = viewModel },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
