using System.Text;
using RestClient.Domain.Auth;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Requests;

public sealed class RequestBuilder
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private const string ContentTypeHeader = "Content-Type";

    private readonly TimeSpan _timeout;

    public RequestBuilder(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    public Result<OutgoingRequest> Build(RequestDefinition definition, IReadOnlyDictionary<string, string> variables)
    {
        if (definition.Auth is not NoAuth)
        {
            return Result<OutgoingRequest>.Failure(Unsupported("Authentication is not supported yet."));
        }

        var url = ResolveUrl(definition, variables);
        if (!url.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(url.Error!);
        }

        var headers = ResolveEntries(HeadersWithContentType(definition), variables);
        if (!headers.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(headers.Error!);
        }

        var content = ResolveBody(definition.Body, variables);
        if (!content.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(content.Error!);
        }

        return Result<OutgoingRequest>.Success(new OutgoingRequest(
            definition.Method,
            url.Value!,
            headers.Value!,
            content.Value,
            _timeout));
    }

    private static Result<string> ResolveUrl(RequestDefinition definition, IReadOnlyDictionary<string, string> variables)
    {
        var baseUrl = VariableResolver.Resolve(definition.Url, variables);
        if (!baseUrl.IsSuccess)
        {
            return baseUrl;
        }

        var query = ResolveEntries(definition.QueryParams, variables);
        if (!query.IsSuccess)
        {
            return Result<string>.Failure(query.Error!);
        }

        return Result<string>.Success(AppendQuery(baseUrl.Value!, query.Value!));
    }

    private static Result<IReadOnlyList<KeyValueEntry>> ResolveEntries(
        IEnumerable<KeyValueEntry> entries,
        IReadOnlyDictionary<string, string> variables)
    {
        var resolved = new List<KeyValueEntry>();
        foreach (var entry in entries.Where(candidate => candidate.IsEnabled))
        {
            var resolvedEntry = ResolveEntry(entry, variables);
            if (!resolvedEntry.IsSuccess)
            {
                return Result<IReadOnlyList<KeyValueEntry>>.Failure(resolvedEntry.Error!);
            }

            resolved.Add(resolvedEntry.Value!);
        }

        return Result<IReadOnlyList<KeyValueEntry>>.Success(resolved);
    }

    private static Result<KeyValueEntry> ResolveEntry(KeyValueEntry entry, IReadOnlyDictionary<string, string> variables)
    {
        var key = VariableResolver.Resolve(entry.Key, variables);
        if (!key.IsSuccess)
        {
            return Result<KeyValueEntry>.Failure(key.Error!);
        }

        var value = VariableResolver.Resolve(entry.Value, variables);
        if (!value.IsSuccess)
        {
            return Result<KeyValueEntry>.Failure(value.Error!);
        }

        return Result<KeyValueEntry>.Success(new KeyValueEntry(key.Value!, value.Value!, true));
    }

    private static Result<ReadOnlyMemory<byte>?> ResolveBody(BodyDefinition body, IReadOnlyDictionary<string, string> variables)
    {
        return body switch
        {
            NoBody => Result<ReadOnlyMemory<byte>?>.Success(null),
            RawBody raw => ResolveRawBody(raw, variables),
            _ => Result<ReadOnlyMemory<byte>?>.Failure(Unsupported("This body type is not supported yet.")),
        };
    }

    private static Result<ReadOnlyMemory<byte>?> ResolveRawBody(RawBody raw, IReadOnlyDictionary<string, string> variables)
    {
        var text = VariableResolver.Resolve(raw.Text, variables);
        if (!text.IsSuccess)
        {
            return Result<ReadOnlyMemory<byte>?>.Failure(text.Error!);
        }

        return Result<ReadOnlyMemory<byte>?>.Success(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(text.Value!)));
    }

    private static IEnumerable<KeyValueEntry> HeadersWithContentType(RequestDefinition definition)
    {
        if (definition.Body is not RawBody { ContentType.Length: > 0 } raw)
        {
            return definition.Headers;
        }

        var otherHeaders = definition.Headers.Where(header => !string.Equals(header.Key, ContentTypeHeader, StringComparison.OrdinalIgnoreCase));
        return otherHeaders.Append(new KeyValueEntry(ContentTypeHeader, raw.ContentType, true));
    }

    private static string AppendQuery(string url, IReadOnlyList<KeyValueEntry> query)
    {
        if (query.Count == 0)
        {
            return url;
        }

        var encoded = string.Join("&", query.Select(entry => $"{Uri.EscapeDataString(entry.Key)}={Uri.EscapeDataString(entry.Value)}"));
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}{encoded}";
    }

    private static Error Unsupported(string message)
    {
        return new Error(ErrorCategory.Validation, message);
    }
}
