using System.Text.Encodings.Web;
using System.Text.Json;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Bodies;

public sealed class JsonBodyFormatter : BodyFormatter
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    protected override Result<string> Reformat(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return Result<string>.Success(JsonSerializer.Serialize(document.RootElement, IndentedOptions));
        }
        catch (JsonException exception)
        {
            return Result<string>.Failure(ResultErrors.Validation(exception.Message));
        }
    }
}
