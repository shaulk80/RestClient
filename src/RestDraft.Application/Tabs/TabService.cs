using RestDraft.Application.Abstractions;
using RestDraft.Application.Collections;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tabs;

// Stores the open tabs with drafts for the ones that have unsaved edits, and restores them on the next start.
public sealed class TabService : ITabService, IDisposable
{
    private readonly IOpenTabsStore _store;
    private readonly IRequestService _requests;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TabService(IOpenTabsStore store, IRequestService requests)
    {
        _store = store;
        _requests = requests;
    }

    public async Task<Result> SaveAsync(IReadOnlyList<TabSnapshot> tabs, CancellationToken cancellationToken)
    {
        var stored = tabs.Select(ToStored).ToList();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await _store.SaveAsync(stored, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result<IReadOnlyList<RestoredTab>>> RestoreAsync(CancellationToken cancellationToken)
    {
        var stored = await _store.LoadAsync(cancellationToken);
        if (!stored.IsSuccess)
        {
            return Result<IReadOnlyList<RestoredTab>>.Failure(stored.Error!);
        }

        var restored = new List<RestoredTab>();
        foreach (var tab in stored.Value!)
        {
            var opened = await RestoreOneAsync(tab, cancellationToken);
            if (opened is not null)
            {
                restored.Add(opened);
            }
        }

        return Result<IReadOnlyList<RestoredTab>>.Success(restored);
    }

    private static OpenTab ToStored(TabSnapshot tab)
    {
        var draft = tab.IsDirty || !tab.IsLinked ? tab.Current : null;
        return new OpenTab(tab.Current.Id, tab.IsLinked, tab.IsSelected, draft);
    }

    // A linked tab whose request was deleted keeps its draft as an unsaved tab. Without a draft there is nothing to show.
    private async Task<RestoredTab?> RestoreOneAsync(OpenTab tab, CancellationToken cancellationToken)
    {
        if (!tab.IsLinked)
        {
            return tab.Draft is null ? null : new RestoredTab(tab.Draft, IsLinked: false, IsDirty: true, tab.IsSelected);
        }

        var saved = await _requests.GetAsync(tab.RequestId, cancellationToken);
        if (!saved.IsSuccess)
        {
            return tab.Draft is null ? null : new RestoredTab(tab.Draft, IsLinked: false, IsDirty: true, tab.IsSelected);
        }

        return tab.Draft is null
            ? new RestoredTab(saved.Value!, IsLinked: true, IsDirty: false, tab.IsSelected)
            : new RestoredTab(tab.Draft with { Name = saved.Value!.Name }, IsLinked: true, IsDirty: true, tab.IsSelected);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
