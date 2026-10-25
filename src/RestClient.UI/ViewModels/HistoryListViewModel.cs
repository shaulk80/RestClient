using System.Collections.ObjectModel;
using System.Windows.Input;
using RestClient.Application.Abstractions;
using RestClient.Application.History;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class HistoryListViewModel : ViewModelBase
{
    public const string AnyMethod = "Any method";

    private readonly IHistoryService _history;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _restoreCommand;
    private HistoryItemViewModel? _selectedItem;
    private string _urlFilter = string.Empty;
    private string _selectedMethod = AnyMethod;
    private string _statusMessage = string.Empty;

    public HistoryListViewModel(IHistoryService history)
    {
        _history = history;
        _searchCommand = new AsyncRelayCommand(ReloadAsync, () => true);
        _restoreCommand = new AsyncRelayCommand(RestoreAsync, () => SelectedItem is not null);
    }

    // Raised with the restored, unsaved request. The window opens it in a new tab.
    public event Action<RequestDefinition>? RestoreRequested;

    public ObservableCollection<HistoryItemViewModel> Items { get; } = [];

    public IReadOnlyList<string> MethodChoices { get; } = [AnyMethod, .. Enum.GetNames<RequestMethod>()];

    public HistoryItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                _restoreCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string UrlFilter
    {
        get => _urlFilter;
        set => SetProperty(ref _urlFilter, value);
    }

    public string SelectedMethod
    {
        get => _selectedMethod;
        set => SetProperty(ref _selectedMethod, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SearchCommand => _searchCommand;

    public ICommand RestoreCommand => _restoreCommand;

    public async Task ReloadAsync()
    {
        var result = await _history.SearchAsync(CreateFilter(), CancellationToken.None);
        if (!result.IsSuccess)
        {
            StatusMessage = ErrorMessageFormatter.Format(result.Error!);
            return;
        }

        Items.Clear();
        foreach (var entry in result.Value!)
        {
            Items.Add(new HistoryItemViewModel(entry));
        }

        SelectedItem = null;
        StatusMessage = string.Empty;
    }

    // Restoring writes nothing to a collection. The request opens unsaved, and Save picks a destination.
    public async Task RestoreAsync()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        var restored = await _history.RestoreAsync(item.Id, CancellationToken.None);
        if (!restored.IsSuccess)
        {
            StatusMessage = ErrorMessageFormatter.Format(restored.Error!);
            return;
        }

        StatusMessage = string.Empty;
        RestoreRequested?.Invoke(restored.Value!);
    }

    private HistoryFilter CreateFilter()
    {
        var method = SelectedMethod == AnyMethod ? (RequestMethod?)null : Enum.Parse<RequestMethod>(SelectedMethod);
        return new HistoryFilter(UrlFilter.Trim(), method);
    }
}
