using RestDraft.Domain.Results;

namespace RestDraft.Application.Tabs;

public interface ITabService
{
    Task<Result> SaveAsync(IReadOnlyList<TabSnapshot> tabs, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<RestoredTab>>> RestoreAsync(CancellationToken cancellationToken);
}
