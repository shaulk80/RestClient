using System.Collections.ObjectModel;
using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IRequestSender _sender;
    private readonly IRequestService _requests;
    private RequestTabViewModel? _selectedTab;

    public MainWindowViewModel(CollectionsTreeViewModel sidebar, IRequestSender sender, IRequestService requests)
    {
        Sidebar = sidebar;
        _sender = sender;
        _requests = requests;
        Sidebar.RequestOpenRequested += OnRequestOpenRequested;
        Sidebar.TreeChanged += OnTreeChanged;
        Tabs.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(HasTabs));
    }

    public CollectionsTreeViewModel Sidebar { get; }

    public ObservableCollection<RequestTabViewModel> Tabs { get; } = [];

    public bool HasTabs => Tabs.Count > 0;

    public RequestTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    public async Task OpenRequestAsync(Guid requestId)
    {
        var existing = Tabs.FirstOrDefault(tab => tab.RequestId == requestId);
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var loaded = await _requests.GetAsync(requestId, CancellationToken.None);
        if (!loaded.IsSuccess)
        {
            Sidebar.StatusMessage = ErrorMessageFormatter.Format(loaded.Error!);
            return;
        }

        var tab = new RequestTabViewModel(_sender, _requests, loaded.Value!);
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    private async void OnRequestOpenRequested(Guid requestId)
    {
        await OpenRequestAsync(requestId);
    }

    // Keeps tab names in step with the tree, and closes tabs whose request was deleted.
    private void OnTreeChanged(object? sender, EventArgs e)
    {
        foreach (var tab in Tabs.ToList())
        {
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
