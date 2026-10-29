using RestDraft.Domain.Collections;

namespace RestDraft.Application.Tabs;

// A tab ready to open. A tab with unsaved edits is restored dirty.
public sealed record RestoredTab(RequestDefinition Definition, bool IsLinked, bool IsDirty, bool IsSelected);
