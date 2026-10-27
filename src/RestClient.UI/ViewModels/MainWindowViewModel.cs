using System.Windows.Input;
using RestClient.Domain.Collections;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly AsyncRelayCommand _settingsCommand;

    public MainWindowViewModel(
        CollectionsTreeViewModel sidebar,
        HistoryListViewModel history,
        TabStripViewModel tabStrip,
        EnvironmentSelectorViewModel environments,
        SecretStatusViewModel secrets,
        ISettingsDialogs settingsDialogs)
    {
        Sidebar = sidebar;
        History = history;
        TabStrip = tabStrip;
        Environments = environments;
        Secrets = secrets;
        _settingsCommand = new AsyncRelayCommand(() => settingsDialogs.ShowAsync(CancellationToken.None), () => true);
        Sidebar.RequestOpenRequested += OnRequestOpenRequested;
        Sidebar.TreeChanged += OnTreeChanged;
        History.RestoreRequested += TabStrip.OpenUnsaved;
        TabStrip.RequestSent += OnTabRequestSent;
        TabStrip.Linked += OnTabLinked;
    }

    public CollectionsTreeViewModel Sidebar { get; }

    public HistoryListViewModel History { get; }

    public TabStripViewModel TabStrip { get; }

    public EnvironmentSelectorViewModel Environments { get; }

    public SecretStatusViewModel Secrets { get; }

    public ICommand SettingsCommand => _settingsCommand;

    public async Task OpenRequestAsync(Guid requestId)
    {
        var opened = await TabStrip.OpenRequestAsync(requestId);
        if (!opened.IsSuccess)
        {
            Sidebar.StatusMessage = ErrorMessageFormatter.Format(opened.Error!);
        }
    }

    public void OpenUnsaved(RequestDefinition definition)
    {
        TabStrip.OpenUnsaved(definition);
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

    private void OnTreeChanged(object? sender, EventArgs e)
    {
        TabStrip.SyncWithTree(requestId => Sidebar.FindRequest(requestId)?.Name);
    }
}
