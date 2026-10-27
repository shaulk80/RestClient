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
    private readonly AsyncRelayCommand _saveAllCommand;
    private readonly RelayCommand _nextRecentTabCommand;
    private readonly RelayCommand _previousRecentTabCommand;
    private readonly RecentTabs _recent = new();
    private readonly HashSet<Guid> _opening = [];
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
        _saveAllCommand = new AsyncRelayCommand(SaveAllAsync, () => true);
        _nextRecentTabCommand = new RelayCommand(() => SwitchRecentOnce(forward: true), () => true);
        _previousRecentTabCommand = new RelayCommand(() => SwitchRecentOnce(forward: false), () => true);
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
                _recent.Select(value);
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

    public ICommand SaveAllCommand => _saveAllCommand;

    // The menu items. A key press goes through SwitchRecent instead, because the key is held while it is repeated.
    public ICommand NextRecentTabCommand => _nextRecentTabCommand;

    public ICommand PreviousRecentTabCommand => _previousRecentTabCommand;

    // Raised after a tab sent a request, including failed and cancelled sends, because each one adds a history entry.
    public event EventHandler? RequestSent;

    // Raised when an unsaved tab was saved into a collection.
    public event EventHandler<RequestLinkedEventArgs>? Linked;

    // Raised when a tab is opened by the user: a new tab, a request from the tree, or a restore from history.
    // It is not raised for the tabs that come back at the start.
    public event EventHandler<RequestTabViewModel>? TabOpened;

    public async Task<Result> OpenRequestAsync(Guid requestId)
    {
        var existing = Tabs.FirstOrDefault(tab => tab.IsLinked && tab.RequestId == requestId);
        if (existing is not null)
        {
            SelectedTab = existing;
            return Result.Success();
        }

        // A second click on the same request while it loads must not open a second tab.
        if (!_opening.Add(requestId))
        {
            return Result.Success();
        }

        try
        {
            var loaded = await _tabServices.Requests.GetAsync(requestId, CancellationToken.None);
            if (!loaded.IsSuccess)
            {
                return Result.Failure(loaded.Error!);
            }

            AddTab(CreateTab(loaded.Value!, isLinked: true, isDirty: false));
            return Result.Success();
        }
        finally
        {
            _opening.Remove(requestId);
        }
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

    // Moves to the next tab in the order the tabs were last used, not the order of the strip. The key stays down while
    // it is repeated, so a run of steps walks further back. CommitRecentSwitch ends the run when the key is released.
    public void SwitchRecent(bool forward)
    {
        _recent.Step(forward, tab => SelectedTab = tab);
    }

    // Makes the tab that a run of SwitchRecent steps stopped on the most recently used one.
    public void CommitRecentSwitch()
    {
        _recent.Commit(SelectedTab);
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
        TabOpened?.Invoke(this, tab);
    }

    private void RemoveTab(RequestTabViewModel tab)
    {
        tab.Cancel();
        tab.RequestSent -= OnTabRequestSent;
        tab.Linked -= OnTabLinked;
        tab.Changed -= OnTabChanged;
        tab.CloseRequested -= OnTabCloseRequested;
        var index = Tabs.IndexOf(tab);
        _recent.Remove(tab);
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

    // Linked tabs first, so cancelling the dialog of an unsaved tab does not keep the others from being saved.
    // An unsaved tab that is still unsaved afterwards means the dialog was cancelled or the save failed: stop there.
    public async Task SaveAllAsync()
    {
        var changed = Tabs.Where(tab => tab.IsDirty).ToList();
        foreach (var tab in changed.Where(tab => tab.IsLinked))
        {
            await tab.SaveAsync();
        }

        foreach (var tab in changed.Where(tab => !tab.IsLinked))
        {
            SelectedTab = tab;
            await tab.SaveAsync();
            if (!tab.IsLinked)
            {
                return;
            }
        }
    }

    private void SwitchRecentOnce(bool forward)
    {
        SwitchRecent(forward);
        CommitRecentSwitch();
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
        foreach (var added in e.NewItems?.OfType<RequestTabViewModel>() ?? [])
        {
            _recent.Add(added);
        }

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
