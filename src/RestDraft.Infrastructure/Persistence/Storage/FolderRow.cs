namespace RestDraft.Infrastructure.Persistence.Storage;

public sealed record FolderRow(Guid Id, Guid CollectionId, Guid? ParentFolderId, string Name);
