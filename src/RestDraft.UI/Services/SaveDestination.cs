namespace RestDraft.UI.Services;

// Where an unsaved request goes. ExpandId is the tree node to expand so the new request is visible.
public sealed record SaveDestination(Guid FolderId, Guid ExpandId, string Name);
