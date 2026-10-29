using RestDraft.Domain.Collections;

namespace RestDraft.Application.History;

// UrlText matches any part of the resolved URL, ignoring case. An empty text matches every URL.
// Method, when set, matches exactly.
public sealed record HistoryFilter(string UrlText, RequestMethod? Method)
{
    public static HistoryFilter All { get; } = new(string.Empty, null);
}
