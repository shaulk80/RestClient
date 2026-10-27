using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using RestClient.Application.Tabs;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

// The open request tabs. Changes to the tabs are stored shortly after they stop, and restored at the next start.
public sealed class TabStripViewModel : ViewModelBase
{
    private const string NewRequestName = "New request";

    private readonly RequestTabServices _tabServices;
    private readonly ITabService _tabService;
    private readonly ISaveChangesPrompt _saveChanges;
    private readonly AutosaveScheduler _autosave;
    private readonly RelayCommand _newTabCommand;
    private readonly AsyncRelayCommand _closeSelectedCommand;
    private readonly AsyncRelayCommand _saveSelectedCommand;
    private readonly AsyncRelayCommand _sendSelectedCommand;
    private RequestTabViewModel? _selectedTab;
    private bool _isRestored;
    private string _autosaveError = string.Empty;

    public TabStripViewModel(
        RequestTabServices tabServices,
        ITabService tabService,
        ISaveChangesPrompt saveChanges,
        AutosaveScheduler autosave)
    {
        _tabServices = tabServices;
        _tabService = tabService;
        _saveChanges = saveChanges;
        _autosave = autosave;
        _newTabCommand = new RelayCommand(NewTab, () => true);
        _closeSelectedCommand = new AsyncRelayCommand(CloseSelectedAsync, () => true);
        _saveSelectedCommand = new AsyncRelayCommand(SaveSelectedAsync, () => true);
        _sendSelectedCommand = new AsyncRelayCommand(SendSelectedAsync, () => true);
        Tabs.CollectionChanged += OnTabsChanged;
    }

    public ObservableCollection<RequestTabViewModel> Tabs { get; } = [];

    public bool HasTabs => Tabs.Count > 0;

    public RequestTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                ScheduleAutosave();
            }
        }
    }

    public string AutosaveError
    {
        get => _autosaveError;
        private set => SetProperty(ref _autosaveError, value);
    }

    public ICommand NewTabCommand => _newTabCommand;

    public ICommand CloseSelectedCommand => _closeSelectedCommand;

    public ICommand SaveSelectedCommand => _saveSelectedCommand;

    public ICommand SendSelectedCommand => _sendSelectedCommand;

    // Raised after a tab sent a request, including failed and cancelled sends, because each one adds a history entry.
    public event EventHandler? RequestSent;

    // Raised when an unsaved tab was saved into a collection.
    public event EventHandler<RequestLinkedEventArgs>? Linked;

    public async Task<Result> OpenRequestAsync(Guid requestId)
    {
        var existing = Tabs.FirstOrDefault(tab => tab.IsLinked && tab.RequestId == requestId);
        if (existing is not null)
        {
            SelectedTab = existing;
            return Result.Success();
        }

        var loaded = await _tabServices.Requests.GetAsync(requestId, CancellationToken.None);
        if (!loaded.IsSuccess)
        {
            return Result.Failure(loaded.Error!);
        }

        AddTab(CreateTab(loaded.Value!, isLinked: true, isDirty: false));
        return Result.Success();
    }

    // Always opens a new tab. Unsaved tabs are never matched against an existing one.
    public void OpenUnsaved(RequestDefinition definition)
    {
        AddTab(CreateTab(definition, isLinked: false, isDirty: true));
    }

    public void NewTab()
    {
        OpenUnsaved(new RequestDefinition { Id = Guid.NewGuid(), Name = NewRequestName });
    }

    // Asks to save a tab with unsaved changes. The tab stays open when the user cancels, or the save does not finish.
    public async Task<bool> CloseTabAsync(RequestTabViewModel tab)
    {
        if (tab.IsDirty && !await ConfirmCloseAsync(tab))
        {
            return false;
        }

        RemoveTab(tab);
        return true;
    }

    // Opens the stored tabs. Storing starts only after this, so a tab the user opens meanwhile cannot replace them.
    public async Task RestoreAsync()
    {
        var restored = await _tabService.RestoreAsync(CancellationToken.None);
        if (restored.IsSuccess)
        {
            AddRestoredTabs(restored.Value!);
        }
        else
        {
            AutosaveError = ErrorMessageFormatter.Format(restored.Error!);
        }

        _isRestored = true;
    }

    // Stores the tabs now, without waiting for the delay. Used when the window closes.
    public Task FlushAsync()
    {
        return _autosave.FlushAsync();
    }

    // Keeps linked tab names in step with the tree, and closes linked tabs whose request was deleted.
    // Unlinked tabs are not in the tree, so they are left alone.
    public void SyncWithTree(Func<Guid, string?> findRequestName)
    {
        foreach (var tab in Tabs.ToList())
        {
            if (!tab.IsLinked)
            {
                continue;
            }

            var name = findRequestName(tab.RequestId);
            if (name is null)
            {
                RemoveTab(tab);
                continue;
            }

            tab.Name = name;
        }
    }

    private void AddRestoredTabs(IReadOnlyList<RestoredTab> restored)
    {
        foreach (var item in restored)
        {
            var tab = CreateTab(item.Definition, item.IsLinked, item.IsDirty);
            Tabs.Add(tab);
            if (item.IsSelected)
            {
                SelectedTab = tab;
            }
        }

        SelectedTab ??= Tabs.LastOrDefault();
    }

    private RequestTabViewModel CreateTab(RequestDefinition definition, bool isLinked, bool isDirty)
    {
        var tab = new RequestTabViewModel(_tabServices, definition, isLinked, isDirty);
        tab.RequestSent += OnTabRequestSent;
        tab.Linked += OnTabLinked;
        tab.Changed += OnTabChanged;
        tab.CloseRequested += OnTabCloseRequested;
        return tab;
    }

    private void AddTab(RequestTabViewModel tab)
    {
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    private void RemoveTab(RequestTabViewModel tab)
    {
        tab.Cancel();
        tab.RequestSent -= OnTabRequestSent;
        tab.Linked -= OnTabLinked;
        tab.Changed -= OnTabChanged;
        tab.CloseRequested -= OnTabCloseRequested;
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (SelectedTab == tab)
        {
            SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        }
    }

    private async Task<bool> ConfirmCloseAsync(RequestTabViewModel tab)
    {
        var choice = await _saveChanges.AskAsync(tab.Name, CancellationToken.None);
        if (choice == SaveChangesChoice.Cancel)
        {
            return false;
        }

        if (choice == SaveChangesChoice.Save)
        {
            await tab.SaveAsync();
        }

        return choice == SaveChangesChoice.Discard || !tab.IsDirty;
    }

    private async Task CloseSelectedAsync()
    {
        if (SelectedTab is { } tab)
        {
            await CloseTabAsync(tab);
        }
    }

    private async Task SaveSelectedAsync()
    {
        if (SelectedTab is { } tab)
        {
            await tab.SaveAsync();
        }
    }

    private async Task SendSelectedAsync()
    {
        if (SelectedTab is { IsSending: false } tab)
        {
            await tab.SendAsync();
        }
    }

    private void ScheduleAutosave()
    {
        if (_isRestored)
        {
            _autosave.Schedule(SaveTabsAsync);
        }
    }

    private async Task SaveTabsAsync()
    {
        var snapshots = Tabs.Select(tab => tab.Snapshot(tab == SelectedTab)).ToList();
        var saved = await _tabService.SaveAsync(snapshots, CancellationToken.None);
        AutosaveError = saved.IsSuccess ? string.Empty : ErrorMessageFormatter.Format(saved.Error!);
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(HasTabs));
        ScheduleAutosave();
    }

    private void OnTabChanged(object? sender, EventArgs e)
    {
        ScheduleAutosave();
    }

    private void OnTabRequestSent(object? sender, EventArgs e)
    {
        RequestSent?.Invoke(this, EventArgs.Empty);
    }

    private void OnTabLinked(object? sender, RequestLinkedEventArgs e)
    {
        Linked?.Invoke(this, e);
        ScheduleAutosave();
    }

    private async void OnTabCloseRequested(object? sender, EventArgs e)
    {
        if (sender is RequestTabViewModel tab)
        {
            await CloseTabAsync(tab);
        }
    }
}
