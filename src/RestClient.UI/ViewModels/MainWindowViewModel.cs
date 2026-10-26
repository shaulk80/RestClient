using System.Collections.ObjectModel;
using RestClient.Application.Collections;
using RestClient.Domain.Collections;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly RequestTabServices _tabServices;
    private RequestTabViewModel? _selectedTab;

    public MainWindowViewModel(
        CollectionsTreeViewModel sidebar,
        HistoryListViewModel history,
        RequestTabServices tabServices,
        EnvironmentSelectorViewModel environments,
        SecretStatusViewModel secrets)
    {
        Sidebar = sidebar;
        History = history;
        Environments = environments;
        Secrets = secrets;
        _tabServices = tabServices;
        Sidebar.RequestOpenRequested += OnRequestOpenRequested;
        Sidebar.TreeChanged += OnTreeChanged;
        History.RestoreRequested += OpenUnsaved;
        Tabs.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(HasTabs));
    }

    public CollectionsTreeViewModel Sidebar { get; }

    public HistoryListViewModel History { get; }

    public EnvironmentSelectorViewModel Environments { get; }

    public SecretStatusViewModel Secrets { get; }

    public ObservableCollection<RequestTabViewModel> Tabs { get; } = [];

    public bool HasTabs => Tabs.Count > 0;

    public RequestTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    public async Task OpenRequestAsync(Guid requestId)
    {
        var existing = Tabs.FirstOrDefault(tab => tab.IsLinked && tab.RequestId == requestId);
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var loaded = await _tabServices.Requests.GetAsync(requestId, CancellationToken.None);
        if (!loaded.IsSuccess)
        {
            Sidebar.StatusMessage = ErrorMessageFormatter.Format(loaded.Error!);
            return;
        }

        AddTab(CreateTab(loaded.Value!, isLinked: true));
    }

    // Always opens a new tab. Unsaved tabs are never matched against an existing one.
    public void OpenUnsaved(RequestDefinition definition)
    {
        AddTab(CreateTab(definition, isLinked: false));
    }

    private RequestTabViewModel CreateTab(RequestDefinition definition, bool isLinked)
    {
        var tab = new RequestTabViewModel(_tabServices, definition, isLinked);
        tab.RequestSent += OnTabRequestSent;
        tab.Linked += OnTabLinked;
        return tab;
    }

    private void AddTab(RequestTabViewModel tab)
    {
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    private async void OnRequestOpenRequested(Guid requestId)
    {
        await OpenRequestAsync(requestId);
    }

    private async void OnTabRequestSent(object? sender, EventArgs e)
    {
        await History.ReloadAsync();
    }

    private async void OnTabLinked(object? sender, RequestLinkedEventArgs e)
    {
        await Sidebar.ReloadAsync(e.RequestId, e.ExpandId);
    }

    // Keeps linked tab names in step with the tree, and closes linked tabs whose request was deleted.
    // Unlinked tabs are not in the tree, so they are left alone.
    private void OnTreeChanged(object? sender, EventArgs e)
    {
        foreach (var tab in Tabs.ToList())
        {
            if (!tab.IsLinked)
            {
                continue;
            }

            var node = Sidebar.FindRequest(tab.RequestId);
            if (node is null)
            {
                CloseTab(tab);
                continue;
            }

            tab.Name = node.Name;
        }
    }

    private void CloseTab(RequestTabViewModel tab)
    {
        Tabs.Remove(tab);
        if (SelectedTab == tab)
        {
            SelectedTab = Tabs.LastOrDefault();
        }
    }
}
