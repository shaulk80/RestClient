using RestClient.Application.Settings;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Requests;

public sealed class RequestBuilder
{
    private const string ContentTypeHeader = "Content-Type";

    private readonly ISettingsService _settings;
    private readonly RequestBodyResolver _bodies;

    public RequestBuilder(ISettingsService settings, RequestBodyResolver bodies)
    {
        _settings = settings;
        _bodies = bodies;
    }

    public async Task<Result<OutgoingRequest>> BuildAsync(
        RequestDefinition definition,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        if (!settings.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(settings.Error!);
        }

        var auth = AuthResolver.Resolve(definition.Auth, variables);
        if (!auth.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(auth.Error!);
        }

        var url = ResolveUrl(definition, variables, auth.Value!.QueryParams);
        if (!url.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(url.Error!);
        }

        var body = await _bodies.ResolveAsync(definition.Body, variables, cancellationToken);
        if (!body.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(body.Error!);
        }

        var headers = ResolveHeaders(definition, variables, auth.Value!, body.Value!);
        if (!headers.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(headers.Error!);
        }

        return Result<OutgoingRequest>.Success(new OutgoingRequest(
            definition.Method,
            url.Value!,
            headers.Value!,
            body.Value!.Content,
            settings.Value!.Timeout));
    }

    private static Result<string> ResolveUrl(
        RequestDefinition definition,
        IReadOnlyDictionary<string, string> variables,
        IReadOnlyList<KeyValueEntry> authQuery)
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

        return Result<string>.Success(AppendQuery(baseUrl.Value!, [.. query.Value!, .. authQuery]));
    }

    // Auth and the body's content type replace a header of the same name that the user wrote.
    private static Result<IReadOnlyList<KeyValueEntry>> ResolveHeaders(
        RequestDefinition definition,
        IReadOnlyDictionary<string, string> variables,
        ResolvedAuth auth,
        ResolvedBody body)
    {
        var headers = KeyValueResolver.Resolve(definition.Headers, variables);
        if (!headers.IsSuccess)
        {
            return Result<IReadOnlyList<KeyValueEntry>>.Failure(headers.Error!);
        }

        var withAuth = auth.Headers.Aggregate(headers.Value!, AddOrReplace);
        var withBody = body.ContentType is null
            ? withAuth
            : AddOrReplace(withAuth, new KeyValueEntry(ContentTypeHeader, body.ContentType, true));
        return Result<IReadOnlyList<KeyValueEntry>>.Success(withBody);
    }

    private static IReadOnlyList<KeyValueEntry> AddOrReplace(IReadOnlyList<KeyValueEntry> headers, KeyValueEntry entry)
    {
        var others = headers.Where(header => !string.Equals(header.Key, entry.Key, StringComparison.OrdinalIgnoreCase)).ToList();
        others.Add(entry);
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
}
