using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Views;

public partial class CollectionsTreeView : UserControl
{
    private static readonly DataFormat<TreeNodeViewModel> NodeFormat = DataFormat.CreateInProcessFormat<TreeNodeViewModel>("RestClient.TreeNode");
    private const double DragThreshold = 4;

    private PointerPressedEventArgs? _pressArgs;
    private TreeNodeViewModel? _pressedNode;
    private Point _pressPoint;
    private TopLevel? _topLevel;

    public CollectionsTreeView()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DoubleTappedEvent, OnDoubleTapped);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        MenuItems.AddHandler(MenuItem.ClickEvent, OnMenuItemClick);
        MenuItems.AddHandler(PointerPressedEvent, OnMenuPointerPressed, RoutingStrategies.Tunnel);
    }

    private CollectionsTreeViewModel? ViewModel => DataContext as CollectionsTreeViewModel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(PointerPressedEvent, OnTopLevelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(PointerPressedEvent, OnTopLevelPointerPressed);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    // Any press in the window closes the menu first. The tree's own handler then runs, and a
    // right-press on a row opens the menu for that row again.
    private void OnTopLevelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsMenuPress(e.Source))
        {
            return;
        }

        CloseMenu();
    }

    private void OnMenuItemClick(object? sender, RoutedEventArgs e)
    {
        CloseMenu();
        Tree.Focus();
    }

    // Escape closes the context menu and returns to the tree. Shift+F10 and the Menu key open it for the
    // selected node, as on any Windows list. Delete deletes the selected node, except in the rename box,
    // where it is an editing key (a KeyBinding on the tree would take it from the box).
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && e.Source is not TextBox && ViewModel?.DeleteSelectedCommand is { } delete)
        {
            e.Handled = true;
            delete.Execute(null);
            return;
        }

        if (e.Key == Key.Escape && MenuPopup.IsOpen)
        {
            e.Handled = true;
            CloseMenu();
            Tree.Focus();
            return;
        }

        var opensMenu = e.Key == Key.Apps || (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift);
        if (opensMenu && ViewModel?.SelectedNode is { IsEditing: false } node)
        {
            e.Handled = true;
            OpenMenu(node, Tree.ContainerFromItem(node) as Control, focusMenu: true);
        }
    }

    // The menu covers the rows below the pointer. A right-press on one of those rows must still select
    // it and move the menu there, so the press is resolved against the tree instead of the menu.
    private void OnMenuPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(MenuItems).Properties.IsRightButtonPressed)
        {
            return;
        }

        var treePoint = e.GetPosition(this);
        if (this.InputHitTest(treePoint) is not { } hit || NodeOf(hit) is not { } node)
        {
            return;
        }

        e.Handled = true;
        OpenMenu(node, hit as Control);
    }

    private void OpenMenu(TreeNodeViewModel node, Control? target, bool focusMenu = false)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.SelectedNode = node;
        MenuPopup.IsOpen = false;
        MenuPopup.DataContext = node;
        MenuPopup.PlacementTarget = target ?? this;
        MenuPopup.IsOpen = true;
        if (focusMenu)
        {
            Dispatcher.UIThread.Post(FocusFirstMenuItem);
        }
    }

    private void FocusFirstMenuItem()
    {
        MenuItems.Items.OfType<MenuItem>().FirstOrDefault(item => item.IsVisible)?.Focus();
    }

    private void CloseMenu()
    {
        MenuPopup.IsOpen = false;
    }

    private static TreeNodeViewModel? NodeOf(object? source)
    {
        return (source as StyledElement)?.DataContext as TreeNodeViewModel;
    }

    // Presses that start inside the menu belong to the menu: a click must reach the menu item, and must
    // not close the menu or start a drag from the tree node the menu happens to be bound to.
    private static bool IsMenuPress(object? source)
    {
        return source is Visual visual && visual.FindAncestorOfType<MenuFlyoutPresenter>(includeSelf: true) is not null;
    }

    // A press on the expander must stay a click. A drag started from it would capture the pointer,
    // and the expand would be lost.
    private static bool IsExpanderPress(object? source)
    {
        return source is Visual visual && visual.FindAncestorOfType<ToggleButton>(includeSelf: true) is not null;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsMenuPress(e.Source))
        {
            _pressedNode = null;
            return;
        }

        var node = NodeOf(e.Source);
        var properties = e.GetCurrentPoint(this).Properties;
        if (node is not null && !node.IsEditing && properties.IsRightButtonPressed)
        {
            _pressedNode = null;
            OpenMenu(node, e.Source as Control);
            return;
        }

        if (node is null || node.IsEditing || IsExpanderPress(e.Source) || !properties.IsLeftButtonPressed)
        {
            _pressedNode = null;
            return;
        }

        _pressedNode = node;
        _pressArgs = e;
        _pressPoint = e.GetPosition(this);
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedNode is null || _pressArgs is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _pressPoint.X) < DragThreshold && Math.Abs(position.Y - _pressPoint.Y) < DragThreshold)
        {
            return;
        }

        var node = _pressedNode;
        var pressArgs = _pressArgs;
        _pressedNode = null;
        _pressArgs = null;
        await StartDragAsync(node, pressArgs);
    }

    // A press that was released without a drag is a click. With the single-click setting on, it opens a request.
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var clicked = _pressedNode;
        _pressedNode = null;
        _pressArgs = null;
        if (clicked is RequestNodeViewModel request && ViewModel is { OpensOnSingleClick: true } viewModel)
        {
            viewModel.OpenRequest(request.Id);
        }
    }

    private static async Task StartDragAsync(TreeNodeViewModel node, PointerPressedEventArgs pressArgs)
    {
        var item = new DataTransferItem();
        item.Set(NodeFormat, node);
        var transfer = new DataTransfer();
        transfer.Add(item);
        await DragDrop.DoDragDropAsync(pressArgs, transfer, DragDropEffects.Move);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(NodeFormat) && NodeOf(e.Source) is not null
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var target = NodeOf(e.Source);
        var source = e.DataTransfer.TryGetValue(NodeFormat);
        if (target is null || source is null || ViewModel is null)
        {
            return;
        }

        e.Handled = true;
        await ViewModel.MoveAsync(source, target);
    }

    // With the single-click setting on, the first click has opened the request already.
    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is { OpensOnSingleClick: false } viewModel && NodeOf(e.Source) is RequestNodeViewModel request)
        {
            viewModel.OpenRequest(request.Id);
        }
    }
}
