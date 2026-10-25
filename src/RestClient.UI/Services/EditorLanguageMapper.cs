namespace RestClient.UI.Services;

public static class EditorLanguageMapper
{
    // Above this size the editor shows plain text, so large bodies stay responsive.
    public const int LargeBodyLimitBytes = 1024 * 1024;

    public static EditorLanguage Map(string? contentType, int byteCount)
    {
        if (byteCount > LargeBodyLimitBytes)
        {
            return EditorLanguage.PlainText;
        }

        return FromContentType(contentType);
    }

    // The language of the content, regardless of size. Formatting uses this, so it works on large bodies too.
    public static EditorLanguage FromContentType(string? contentType)
    {
        var mediaType = (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        if (mediaType is "application/json" or "text/json" || mediaType.EndsWith("+json", StringComparison.Ordinal))
        {
            return EditorLanguage.Json;
        }

        if (mediaType is "application/xml" or "text/xml" || mediaType.EndsWith("+xml", StringComparison.Ordinal))
        {
            return EditorLanguage.Xml;
        }

        return EditorLanguage.PlainText;
    }
}
