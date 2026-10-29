using RestDraft.Domain.Collections;

namespace RestDraft.Application.History;

// The URL a history entry shows and is searched by: the URL and enabled query parameters as the user wrote them,
// with {{placeholders}} left in place. A resolved URL could carry secret values, and history is stored unencrypted.
public static class HistoryUrl
{
    public static string For(RequestDefinition definition)
    {
        var query = definition.QueryParams
            .Where(parameter => parameter.IsEnabled)
            .Select(parameter => $"{parameter.Key}={parameter.Value}")
            .ToList();
        return query.Count == 0
            ? definition.Url
            : $"{definition.Url}?{string.Join('&', query)}";
    }
}
