using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;
using RestDraft.UI.Views;

namespace RestDraft.UI;

public partial class MainWindow : Window
{
    private const double SplitterWidth = 6;
    private const double SidebarMinWidth = 160;
    private const double DetailsMinWidth = 280;
    private const double ResponseMinWidth = 200;

    private readonly TextEditTarget _editTarget = new();
    private MainWindowViewModel? _viewModel;
    private bool _tabsStored;

    public MainWindow()
    {
        InitializeComponent();
        // Ctrl+Tab is taken before any control sees it. The tab controls would switch their own tabs otherwise,
        // and the code editor would type a tab.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(GotFocusEvent, OnGotFocus);
        Deactivated += OnDeactivated;
        Panels.SizeChanged += (_, _) => LimitPanelWidths();
        RequestPanels.SizeChanged += (_, _) => LimitPanelWidths();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel.Layout.PropertyChanged -= OnLayoutChanged;
            _viewModel.TabStrip.TabOpened -= OnTabOpened;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelChanged;
            _viewModel.Layout.PropertyChanged += OnLayoutChanged;
            _viewModel.TabStrip.TabOpened += OnTabOpened;
            ApplyLayout();
        }
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_viewModel is { } viewModel)
        {
            await viewModel.ApplySettingsAsync();
            await viewModel.Sidebar.ReloadAsync();
            await viewModel.History.ReloadAsync();
            await viewModel.Environments.ReloadAsync();
            await viewModel.Secrets.ReloadAsync();
            await viewModel.TabStrip.RestoreAsync();
        }
    }

    // The open tabs are stored a moment after each change. Closing waits for the last store, so no edit is lost.
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_tabsStored || _viewModel is not { } viewModel)
        {
            return;
        }

        e.Cancel = true;
        await RememberPanelWidthsAsync();
        await viewModel.RememberVisibilityAsync();
        await viewModel.TabStrip.FlushAsync();
        _tabsStored = true;
        Close();
    }

    // The details view is shared by all tabs, so the new tab's body is on screen only after the next layout pass.
    private void OnTabOpened(object? sender, RequestTabViewModel e)
    {
        Dispatcher.UIThread.Post(Details.FocusBody, DispatcherPriority.Background);
    }

    // The theme belongs to the whole application, so the dialogs follow it too.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.Theme) && Avalonia.Application.Current is { } application && _viewModel is { } viewModel)
        {
            application.RequestedThemeVariant = ThemeVariants.For(viewModel.Theme);
        }
    }

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        ApplyLayout();
    }

    // The splitters change the columns themselves, so the columns are set from the view model only when it changes:
    // at the start, when a panel is shown or hidden, or after a drag.
    private void ApplyLayout()
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        var layout = viewModel.Layout;
        var sidebar = Panels.ColumnDefinitions[0];
        sidebar.MinWidth = layout.IsSidebarVisible ? SidebarMinWidth : 0;
        sidebar.Width = new GridLength(layout.IsSidebarVisible ? layout.SidebarWidth : 0);
        var response = RequestPanels.ColumnDefinitions[2];
        response.MinWidth = layout.IsResponseVisible ? ResponseMinWidth : 0;
        response.Width = new GridLength(layout.IsResponseVisible ? layout.ResponseWidth : 0);
        LimitPanelWidths();
    }

    // The request area keeps its minimum width, so a wide panel cannot push the others out of a small window.
    private void LimitPanelWidths()
    {
        var sidebarRoom = Panels.Bounds.Width - SplitterWidth - DetailsMinWidth - SplitterWidth - ResponseMinWidth;
        Panels.ColumnDefinitions[0].MaxWidth = Math.Max(SidebarMinWidth, sidebarRoom);
        var responseRoom = RequestPanels.Bounds.Width - SplitterWidth - DetailsMinWidth;
        RequestPanels.ColumnDefinitions[2].MaxWidth = Math.Max(ResponseMinWidth, responseRoom);
    }

    private async void OnSidebarSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        await RememberPanelWidthsAsync();
    }

    private async void OnResponseSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        await RememberPanelWidthsAsync();
    }

    // Also runs when the window closes, because a splitter moved with the arrow keys has no drag to complete.
    // A hidden panel has no width to remember, and a width that did not change is not stored again.
    private async Task RememberPanelWidthsAsync()
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        var layout = viewModel.Layout;
        var sidebarWidth = Panels.ColumnDefinitions[0].ActualWidth;
        if (layout.IsSidebarVisible && Math.Abs(sidebarWidth - layout.SidebarWidth) >= 1)
        {
            await viewModel.RememberSidebarWidthAsync(sidebarWidth);
        }

        var responseWidth = RequestPanels.ColumnDefinitions[2].ActualWidth;
        if (layout.IsResponseVisible && Math.Abs(responseWidth - layout.ResponseWidth) >= 1)
        {
            await viewModel.RememberResponseWidthAsync(responseWidth);
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || !e.KeyModifiers.HasFlag(KeyModifiers.Control) || _viewModel is not { } viewModel)
        {
            return;
        }

        e.Handled = true;
        viewModel.TabStrip.SwitchRecent(forward: !e.KeyModifiers.HasFlag(KeyModifiers.Shift));
    }

    // Releasing Ctrl ends a run of Ctrl+Tab presses. The tab it stopped on becomes the most recently used one.
    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            _viewModel?.TabStrip.CommitRecentSwitch();
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        _viewModel?.TabStrip.CommitRecentSwitch();
    }

    private void OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _editTarget.Remember(e.Source);
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCutClick(object? sender, RoutedEventArgs e)
    {
        _editTarget.Cut();
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        _editTarget.Copy();
    }

    private void OnPasteClick(object? sender, RoutedEventArgs e)
    {
        _editTarget.Paste();
    }

    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        _editTarget.SelectAll();
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        await new AboutWindow().ShowDialog(this);
    }

    // A cleared list also clears the selection. Only a real choice is saved as the active environment.
    private async void OnEnvironmentSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is { } viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is EnvironmentChoice choice)
        {
            await viewModel.Environments.SelectAsync(choice);
        }
    }
}
