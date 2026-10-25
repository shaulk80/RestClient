using RestClient.Domain.Auth;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Requests;

public sealed class RequestBuilder
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private const string ContentTypeHeader = "Content-Type";

    private readonly TimeSpan _timeout;
    private readonly RequestBodyResolver _bodies;

    public RequestBuilder(TimeSpan timeout, RequestBodyResolver bodies)
    {
        _timeout = timeout;
        _bodies = bodies;
    }

    public async Task<Result<OutgoingRequest>> BuildAsync(
        RequestDefinition definition,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
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

        var body = await _bodies.ResolveAsync(definition.Body, variables, cancellationToken);
        if (!body.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(body.Error!);
        }

        var headers = KeyValueResolver.Resolve(definition.Headers, variables);
        if (!headers.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(headers.Error!);
        }

        return Result<OutgoingRequest>.Success(new OutgoingRequest(
            definition.Method,
            url.Value!,
            WithContentType(headers.Value!, body.Value!.ContentType),
            body.Value!.Content,
            _timeout));
    }

    private static Result<string> ResolveUrl(RequestDefinition definition, IReadOnlyDictionary<string, string> variables)
    {
        var baseUrl = VariableResolver.Resolve(definition.Url, variables);
        if (!baseUrl.IsSuccess)
        {
            return baseUrl;
        }

        var query = KeyValueResolver.Resolve(definition.QueryParams, variables);
        if (!query.IsSuccess)
        {
            return Result<string>.Failure(query.Error!);
        }

        return Result<string>.Success(AppendQuery(baseUrl.Value!, query.Value!));
    }

    // A body that sets its own content type replaces any Content-Type header the user wrote.
    private static IReadOnlyList<KeyValueEntry> WithContentType(IReadOnlyList<KeyValueEntry> headers, string? contentType)
    {
        if (contentType is null)
        {
            return headers;
        }

        var others = headers.Where(header => !string.Equals(header.Key, ContentTypeHeader, StringComparison.OrdinalIgnoreCase)).ToList();
        others.Add(new KeyValueEntry(ContentTypeHeader, contentType, true));
        return others;
    }

    private static string AppendQuery(string url, IReadOnlyList<KeyValueEntry> query)
    {
        if (query.Count == 0)
        {
            return url;
        }

        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}{KeyValueEncoder.Encode(query)}";
    }

    private static Error Unsupported(string message)
    {
        return new Error(ErrorCategory.Validation, message);
    }
}
