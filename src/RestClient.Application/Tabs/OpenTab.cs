using RestClient.Domain.Collections;

namespace RestClient.Application.Tabs;

// A stored tab. RequestId is the saved request the tab is linked to, or the draft's own identifier for a tab that
// is not in any collection. Draft holds the tab's unsaved edits, and is null for a clean linked tab.
public sealed record OpenTab(Guid RequestId, bool IsLinked, bool IsSelected, RequestDefinition? Draft);
