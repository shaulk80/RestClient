using System.Collections.ObjectModel;
using System.Windows.Input;
using RestClient.Domain.Bodies;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class MultipartEditorViewModel
{
    private readonly IFilePickerService _files;

    public MultipartEditorViewModel(IFilePickerService files, IEnumerable<MultipartPart> parts)
    {
        _files = files;
        foreach (var part in parts)
        {
            Rows.Add(CreateRow(part));
        }

        AddRowCommand = new RelayCommand(AddRow, () => true);
    }

    public event EventHandler? Changed;

    public ObservableCollection<MultipartRowViewModel> Rows { get; } = [];

    public ICommand AddRowCommand { get; }

    public IReadOnlyList<MultipartPart> ToParts()
    {
        return Rows.Select(row => row.ToPart()).ToList();
    }

    private void AddRow()
    {
        Rows.Add(CreateRow(new MultipartPart(string.Empty, string.Empty, null)));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveRow(MultipartRowViewModel row)
    {
        Rows.Remove(row);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private MultipartRowViewModel CreateRow(MultipartPart part)
    {
        var row = new MultipartRowViewModel(_files, part);
        row.Changed += OnRowChanged;
        row.RemoveRequested += (_, _) => RemoveRow(row);
        return row;
    }

    private void OnRowChanged(object? sender, EventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
