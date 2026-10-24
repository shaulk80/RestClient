using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Views;

public partial class CollectionsTreeView : UserControl
{
    private static readonly DataFormat<TreeNodeViewModel> NodeFormat = DataFormat.CreateInProcessFormat<TreeNodeViewModel>("RestClient.TreeNode");
    private const double DragThreshold = 4;

    private PointerPressedEventArgs? _pressArgs;
    private TreeNodeViewModel? _pressedNode;
    private Point _pressPoint;

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
    }

    private CollectionsTreeViewModel? ViewModel => DataContext as CollectionsTreeViewModel;

    private static TreeNodeViewModel? NodeOf(object? source)
    {
        return (source as StyledElement)?.DataContext as TreeNodeViewModel;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var node = NodeOf(e.Source);
        if (node is null || node.IsEditing || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
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

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressedNode = null;
        _pressArgs = null;
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

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (NodeOf(e.Source) is RequestNodeViewModel request)
        {
            ViewModel?.OpenRequest(request.Id);
        }
    }
}
