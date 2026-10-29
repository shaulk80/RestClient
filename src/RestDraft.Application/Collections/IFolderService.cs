using RestDraft.Domain.Results;

namespace RestDraft.Application.Collections;

public interface IFolderService
{
    Task<Result<Guid>> CreateAsync(Guid parentFolderId, CancellationToken cancellationToken);

    Task<Result> RenameAsync(Guid folderId, string name, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(Guid folderId, CancellationToken cancellationToken);
}
