using System.Xml;
using System.Xml.Linq;
using RestClient.Domain.Results;

namespace RestClient.Application.Bodies;

public sealed class XmlBodyFormatter : BodyFormatter
{
    protected override Result<string> Reformat(string text)
    {
        try
        {
            var document = XDocument.Parse(text);
            return Result<string>.Success(Serialize(document));
        }
        catch (XmlException exception)
        {
            return Result<string>.Failure(ResultErrors.Validation(exception.Message));
        }
    }

    // ToString indents the content but drops the declaration, so the declaration is written back when the input had one.
    private static string Serialize(XDocument document)
    {
        var declaration = document.Declaration is null
            ? string.Empty
            : $"{document.Declaration}{Environment.NewLine}";
        return $"{declaration}{document}";
    }
}
