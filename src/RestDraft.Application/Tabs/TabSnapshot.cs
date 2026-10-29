using RestDraft.Domain.Collections;

namespace RestDraft.Application.Tabs;

// What the UI reports about one open tab: its current content, and whether it differs from the saved request.
public sealed record TabSnapshot(RequestDefinition Current, bool IsLinked, bool IsDirty, bool IsSelected);
