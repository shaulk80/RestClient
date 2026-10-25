using System.Windows.Input;
using RestClient.Domain.Bodies;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class MultipartRowViewModel : ViewModelBase
{
    private readonly IFilePickerService _files;
    private string _name;
    private string _value;
    private string _filePath;
    private bool _isFile;
    private string _errorMessage = string.Empty;

    public MultipartRowViewModel(IFilePickerService files, MultipartPart part)
    {
        _files = files;
        _name = part.Name;
        _value = part.Value ?? string.Empty;
        _filePath = part.FilePath ?? string.Empty;
        _isFile = part.FilePath is not null;
        BrowseCommand = new AsyncRelayCommand(BrowseAsync, () => true);
        RemoveCommand = new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty), () => true);
    }

    public event EventHandler? Changed;

    public event EventHandler? RemoveRequested;

    public string Name
    {
        get => _name;
        set => EditField(ref _name, value, nameof(Name));
    }

    public string Value
    {
        get => _value;
        set => EditField(ref _value, value, nameof(Value));
    }

    public string FilePath
    {
        get => _filePath;
        set => EditField(ref _filePath, value, nameof(FilePath));
    }

    public bool IsFile
    {
        get => _isFile;
        set => EditField(ref _isFile, value, nameof(IsFile));
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand BrowseCommand { get; }

    public ICommand RemoveCommand { get; }

    public MultipartPart ToPart()
    {
        return _isFile
            ? new MultipartPart(_name, null, _filePath)
            : new MultipartPart(_name, _value, null);
    }

    public async Task BrowseAsync()
    {
        var picked = await _files.PickFileAsync();
        if (!picked.IsSuccess)
        {
            ErrorMessage = ErrorMessageFormatter.Format(picked.Error!);
            return;
        }

        if (picked.Value is not { } path)
        {
            return;
        }

        ErrorMessage = string.Empty;
        FilePath = path;
        IsFile = true;
    }

    private void EditField<T>(ref T field, T value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
