using RestDraft.Domain.Auth;
using RestDraft.Domain.Bodies;
using RestDraft.Domain.Collections;

namespace RestDraft.Application.Requests;

// Finds every {{name}} that a request refers to, in its URL, parameters, headers, body and auth.
public static class RequestPlaceholders
{
    public static IReadOnlySet<string> NamesIn(RequestDefinition definition)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in TextsOf(definition))
        {
            if (text is not null)
            {
                names.UnionWith(VariableResolver.PlaceholderNames(text));
            }
        }

        return names;
    }

    private static IEnumerable<string?> TextsOf(RequestDefinition definition)
    {
        yield return definition.Url;
        foreach (var entry in definition.QueryParams.Concat(definition.Headers))
        {
            yield return entry.Key;
            yield return entry.Value;
        }

        foreach (var text in BodyTexts(definition.Body))
        {
            yield return text;
        }

        foreach (var text in AuthTexts(definition.Auth))
        {
            yield return text;
        }
    }

    private static IEnumerable<string?> BodyTexts(BodyDefinition body)
    {
        return body switch
        {
            RawBody raw => [raw.Text, raw.ContentType],
            FormUrlEncodedBody form => form.Fields.SelectMany<KeyValueEntry, string?>(entry => [entry.Key, entry.Value]),
            MultipartBody multipart => multipart.Parts.SelectMany<MultipartPart, string?>(part => [part.Name, part.Value, part.FilePath]),
            _ => [],
        };
    }

    private static IEnumerable<string?> AuthTexts(AuthDefinition auth)
    {
        return auth switch
        {
            BasicAuth basic => [basic.Username, basic.Password],
            BearerAuth bearer => [bearer.Token],
            ApiKeyAuth apiKey => [apiKey.Key, apiKey.Value],
            _ => [],
        };
    }
}
