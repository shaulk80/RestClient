using System.Collections.ObjectModel;
using System.Windows.Input;
using RestClient.Domain.Collections;
using RestClient.UI.Commands;

namespace RestClient.UI.ViewModels;

public sealed class KeyValueEditorViewModel
{
    public KeyValueEditorViewModel(IEnumerable<KeyValueEntry> entries)
    {
        foreach (var entry in entries)
        {
            Rows.Add(CreateRow(entry));
        }

        AddRowCommand = new RelayCommand(AddRow, () => true);
    }

    public event EventHandler? Changed;

    public ObservableCollection<KeyValueRowViewModel> Rows { get; } = [];

    public ICommand AddRowCommand { get; }

    public IReadOnlyList<KeyValueEntry> ToEntries()
    {
        return Rows.Select(row => row.ToEntry()).ToList();
    }

    private void AddRow()
    {
        Rows.Add(CreateRow(new KeyValueEntry(string.Empty, string.Empty, true)));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveRow(KeyValueRowViewModel row)
    {
        Rows.Remove(row);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private KeyValueRowViewModel CreateRow(KeyValueEntry entry)
    {
        var row = new KeyValueRowViewModel(entry);
        row.Changed += OnRowChanged;
        row.RemoveRequested += (_, _) => RemoveRow(row);
        return row;
    }

    private void OnRowChanged(object? sender, EventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
