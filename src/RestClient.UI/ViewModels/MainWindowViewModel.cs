using System.Windows.Input;
using RestClient.Application.Settings;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ISettingsDialogs _settingsDialogs;
    private readonly AsyncRelayCommand _settingsCommand;

    public MainWindowViewModel(
        CollectionsTreeViewModel sidebar,
        HistoryListViewModel history,
        TabStripViewModel tabStrip,
        EnvironmentSelectorViewModel environments,
        SecretStatusViewModel secrets,
        WorkspaceLayoutViewModel layout,
        ISettingsService settings,
        ISettingsDialogs settingsDialogs)
    {
        Sidebar = sidebar;
        History = history;
        TabStrip = tabStrip;
        Environments = environments;
        Secrets = secrets;
        Layout = layout;
        _settings = settings;
        _settingsDialogs = settingsDialogs;
        _settingsCommand = new AsyncRelayCommand(ShowSettingsAsync, () => true);
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

    public WorkspaceLayoutViewModel Layout { get; }

    public ICommand SettingsCommand => _settingsCommand;

    // Takes the settings that the window itself uses: the panel widths and the click that opens a request.
    public async Task ApplySettingsAsync()
    {
        var loaded = await _settings.GetAsync(CancellationToken.None);
        if (!loaded.IsSuccess)
        {
            Sidebar.StatusMessage = ErrorMessageFormatter.Format(loaded.Error!);
            return;
        }

        Layout.Apply(loaded.Value!);
        Sidebar.OpensOnSingleClick = loaded.Value!.OpenOnSingleClick;
    }

    public async Task RememberSidebarWidthAsync(double width)
    {
        ReportFailure(await Layout.RememberSidebarWidthAsync(width));
    }

    public async Task RememberResponseWidthAsync(double width)
    {
        ReportFailure(await Layout.RememberResponseWidthAsync(width));
    }

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

    private async Task ShowSettingsAsync()
    {
        await _settingsDialogs.ShowAsync(CancellationToken.None);
        await ApplySettingsAsync();
    }

    private void ReportFailure(Result result)
    {
        if (!result.IsSuccess)
        {
            Sidebar.StatusMessage = ErrorMessageFormatter.Format(result.Error!);
        }
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
