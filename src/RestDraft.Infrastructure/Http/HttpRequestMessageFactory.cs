using RestDraft.Application.Requests;
using RestDraft.Domain.Collections;

namespace RestDraft.Infrastructure.Http;

public static class HttpRequestMessageFactory
{
    private const string ContentHeaderPrefix = "Content-";

    public static HttpRequestMessage Create(OutgoingRequest request)
    {
        var message = new HttpRequestMessage(MapMethod(request.Method), new Uri(request.Url, UriKind.Absolute));
        if (request.Content is { } content)
        {
            message.Content = new ReadOnlyMemoryContent(content);
        }

        foreach (var header in request.Headers)
        {
            AddHeader(message, header);
        }

        return message;
    }

    private static void AddHeader(HttpRequestMessage message, KeyValueEntry header)
    {
        if (header.Key.StartsWith(ContentHeaderPrefix, StringComparison.OrdinalIgnoreCase) && message.Content is { } content)
        {
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            return;
        }

        message.Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    private static HttpMethod MapMethod(RequestMethod method)
    {
        return method switch
        {
            RequestMethod.Get => HttpMethod.Get,
            RequestMethod.Post => HttpMethod.Post,
            RequestMethod.Put => HttpMethod.Put,
            RequestMethod.Patch => HttpMethod.Patch,
            RequestMethod.Delete => HttpMethod.Delete,
            RequestMethod.Head => HttpMethod.Head,
            RequestMethod.Options => HttpMethod.Options,
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown request method."),
        };
    }
}
