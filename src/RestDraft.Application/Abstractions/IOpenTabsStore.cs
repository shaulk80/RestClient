using RestDraft.Application.Tabs;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

// Holds the list of open tabs. Each save replaces the whole list.
public interface IOpenTabsStore
{
    Task<Result<IReadOnlyList<OpenTab>>> LoadAsync(CancellationToken cancellationToken);

    Task<Result> SaveAsync(IReadOnlyList<OpenTab> tabs, CancellationToken cancellationToken);
}
