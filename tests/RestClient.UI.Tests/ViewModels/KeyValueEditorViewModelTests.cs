using NUnit.Framework;
using RestClient.Domain.Collections;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class KeyValueEditorViewModelTests
{
    [Test]
    public void ToEntriesReturnsRowsInOrder()
    {
        var editor = new KeyValueEditorViewModel([new KeyValueEntry("a", "1", true), new KeyValueEntry("b", "2", false)]);
        KeyValueEntry[] expected = [new KeyValueEntry("a", "1", true), new KeyValueEntry("b", "2", false)];

        Assert.That(editor.ToEntries(), Is.EqualTo(expected));
    }

    [Test]
    public void AddRowAppendsEnabledEmptyRow()
    {
        var editor = new KeyValueEditorViewModel([]);
        KeyValueEntry[] expected = [new KeyValueEntry(string.Empty, string.Empty, true)];

        editor.AddRowCommand.Execute(null);

        Assert.That(editor.Rows.Count, Is.EqualTo(1));
        Assert.That(editor.ToEntries(), Is.EqualTo(expected));
    }

    [Test]
    public void RemoveRowRemovesIt()
    {
        var editor = new KeyValueEditorViewModel([new KeyValueEntry("a", "1", true)]);

        editor.Rows[0].RemoveCommand.Execute(null);

        Assert.That(editor.Rows, Is.Empty);
    }

    [Test]
    public void EditingRowUpdatesEntriesAndRaisesChanged()
    {
        var editor = new KeyValueEditorViewModel([new KeyValueEntry("a", "1", true)]);
        KeyValueEntry[] expected = [new KeyValueEntry("a", "2", false)];
        var raised = 0;
        editor.Changed += (_, _) => raised++;

        editor.Rows[0].Value = "2";
        editor.Rows[0].IsEnabled = false;

        Assert.That(editor.ToEntries(), Is.EqualTo(expected));
        Assert.That(raised, Is.EqualTo(2));
    }
}
