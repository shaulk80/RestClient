namespace RestClient.UI.ViewModels;

// A folder the save dialog can target. Path reads "Collection / Folder / Subfolder".
public sealed record FolderChoice(Guid FolderId, Guid ExpandId, string Path);
