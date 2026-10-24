using System.Text.Json;
using System.Text.Json.Serialization;
using RestClient.Infrastructure.Persistence.Dto;

namespace RestClient.Infrastructure.Persistence.Storage;

public static class RequestJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(RequestDto request)
    {
        return JsonSerializer.Serialize(request, Options);
    }

    public static RequestDto? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<RequestDto>(json, Options);
    }
}
