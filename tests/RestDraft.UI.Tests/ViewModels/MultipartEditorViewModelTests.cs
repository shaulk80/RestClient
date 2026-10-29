using FakeItEasy;
using NUnit.Framework;
using RestDraft.Domain.Bodies;
using RestDraft.Domain.Results;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

[TestFixture]
public class MultipartEditorViewModelTests
{
    private const string FilePath = "C:\\data\\input.txt";

    [Test]
    public void ToPartsUsesValueForTextRowsAndFilePathForFileRows()
    {
        var editor = new MultipartEditorViewModel(A.Fake<IFilePickerService>(), [new MultipartPart("title", "x", null)]);
        MultipartPart[] expected = [new MultipartPart("title", "x", null), new MultipartPart("file", null, FilePath)];
        editor.AddRowCommand.Execute(null);
        editor.Rows[1].Name = "file";
        editor.Rows[1].IsFile = true;
        editor.Rows[1].FilePath = FilePath;

        Assert.That(editor.ToParts(), Is.EqualTo(expected));
    }

    [Test]
    public async Task BrowseSetsFilePathAndSelectsFileRow()
    {
        var files = A.Fake<IFilePickerService>();
        A.CallTo(() => files.PickFileAsync()).Returns(Task.FromResult(Result<string?>.Success(FilePath)));
        var editor = new MultipartEditorViewModel(files, [new MultipartPart("file", null, null)]);

        await editor.Rows[0].BrowseAsync();

        Assert.That(editor.Rows[0].IsFile, Is.True);
        Assert.That(editor.Rows[0].FilePath, Is.EqualTo(FilePath));
        Assert.That(editor.Rows[0].ErrorMessage, Is.Empty);
    }

    [Test]
    public async Task BrowseLeavesRowUnchangedWhenDialogIsCancelled()
    {
        var files = A.Fake<IFilePickerService>();
        A.CallTo(() => files.PickFileAsync()).Returns(Task.FromResult(Result<string?>.Success(null)));
        var editor = new MultipartEditorViewModel(files, [new MultipartPart("file", "value", null)]);

        await editor.Rows[0].BrowseAsync();

        Assert.That(editor.Rows[0].IsFile, Is.False);
        Assert.That(editor.Rows[0].FilePath, Is.Empty);
    }

    [Test]
    public async Task BrowseShowsErrorInRowWhenDialogFails()
    {
        var files = A.Fake<IFilePickerService>();
        A.CallTo(() => files.PickFileAsync()).Returns(Task.FromResult(Result<string?>.Failure(new Error(ErrorCategory.Unexpected, "No window."))));
        var editor = new MultipartEditorViewModel(files, [new MultipartPart("file", null, null)]);

        await editor.Rows[0].BrowseAsync();

        Assert.That(editor.Rows[0].ErrorMessage, Is.EqualTo("No window."));
    }

    [Test]
    public void RemoveRowRemovesPart()
    {
        var editor = new MultipartEditorViewModel(A.Fake<IFilePickerService>(), [new MultipartPart("a", "1", null)]);

        editor.Rows[0].RemoveCommand.Execute(null);

        Assert.That(editor.Rows, Is.Empty);
    }
}
