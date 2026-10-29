using RestDraft.Domain.Collections;

namespace RestDraft.UI.ViewModels;

// A folder the save dialog can target. Path reads "Collection / Folder / Subfolder".
// Folder is kept to check a chosen name against the names already used in it.
public sealed record FolderChoice(Guid FolderId, Guid ExpandId, string Path, Folder Folder);
