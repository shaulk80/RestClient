using RestDraft.Application.Bodies;
using RestDraft.Domain.Results;

namespace RestDraft.UI.Services;

public sealed class BodyFormatting
{
    private readonly JsonBodyFormatter _json;
    private readonly XmlBodyFormatter _xml;

    public BodyFormatting(JsonBodyFormatter json, XmlBodyFormatter xml)
    {
        _json = json;
        _xml = xml;
    }

    public static bool CanFormat(EditorLanguage language)
    {
        return language is EditorLanguage.Json or EditorLanguage.Xml;
    }

    public Result<string> Format(EditorLanguage language, string text)
    {
        return language switch
        {
            EditorLanguage.Json => _json.Format(text),
            EditorLanguage.Xml => _xml.Format(text),
            _ => Result<string>.Failure(ResultErrors.Validation("This body cannot be formatted.")),
        };
    }
}
