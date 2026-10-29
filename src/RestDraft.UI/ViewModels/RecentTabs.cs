namespace RestDraft.UI.ViewModels;

// Keeps the open tabs in the order they were last used, most recent first. A run of steps (Ctrl+Tab with Ctrl held)
// walks back through that order without changing it. Only Commit, when the key is released, moves the tab where the
// run stopped to the front.
public sealed class RecentTabs
{
    private readonly List<RequestTabViewModel> _order = [];
    private List<RequestTabViewModel>? _run;
    private int _runIndex;
    private bool _isStepping;

    public IReadOnlyList<RequestTabViewModel> Order => _order;

    public void Add(RequestTabViewModel tab)
    {
        if (!_order.Contains(tab))
        {
            _order.Add(tab);
        }
    }

    public void Remove(RequestTabViewModel tab)
    {
        _order.Remove(tab);
        _run = null;
    }

    // A tab the user picked, or a tab that was just opened. A selection made by Step is not one of them.
    public void Select(RequestTabViewModel? tab)
    {
        if (_isStepping || tab is null)
        {
            return;
        }

        _run = null;
        MoveToFront(tab);
    }

    public void Step(bool forward, Action<RequestTabViewModel> select)
    {
        if (_order.Count < 2)
        {
            return;
        }

        if (_run is null)
        {
            _run = [.. _order];
            _runIndex = 0;
        }

        var direction = forward ? 1 : -1;
        _runIndex = (_runIndex + direction + _run.Count) % _run.Count;
        _isStepping = true;
        try
        {
            select(_run[_runIndex]);
        }
        finally
        {
            _isStepping = false;
        }
    }

    public void Commit(RequestTabViewModel? selected)
    {
        if (_run is null)
        {
            return;
        }

        _run = null;
        if (selected is not null)
        {
            MoveToFront(selected);
        }
    }

    private void MoveToFront(RequestTabViewModel tab)
    {
        _order.Remove(tab);
        _order.Insert(0, tab);
    }
}
