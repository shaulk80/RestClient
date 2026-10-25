using System.Text;
using RestClient.Application.Abstractions;
using RestClient.Domain.Bodies;
using RestClient.Domain.Results;

namespace RestClient.Application.Requests;

public sealed class RequestBodyResolver
{
    public const string FormUrlEncodedContentType = "application/x-www-form-urlencoded";

    private const string MultipartBoundaryPrefix = "RestClientBoundary";
    private const string MultipartContentTypePrefix = "multipart/form-data; boundary=";

    private readonly MultipartBodyEncoder _multipart;
    private readonly IIdGenerator _ids;

    public RequestBodyResolver(MultipartBodyEncoder multipart, IIdGenerator ids)
    {
        _multipart = multipart;
        _ids = ids;
    }

    public async Task<Result<ResolvedBody>> ResolveAsync(
        BodyDefinition body,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        return body switch
        {
            NoBody => Result<ResolvedBody>.Success(ResolvedBody.Empty),
            RawBody raw => ResolveRaw(raw, variables),
            FormUrlEncodedBody form => ResolveForm(form, variables),
            MultipartBody multipart => await ResolveMultipartAsync(multipart, variables, cancellationToken),
            _ => Result<ResolvedBody>.Failure(ResultErrors.Validation("This body type is not supported.")),
        };
    }

    private static Result<ResolvedBody> ResolveRaw(RawBody raw, IReadOnlyDictionary<string, string> variables)
    {
        var text = VariableResolver.Resolve(raw.Text, variables);
        if (!text.IsSuccess)
        {
            return Result<ResolvedBody>.Failure(text.Error!);
        }

        var contentType = VariableResolver.Resolve(raw.ContentType, variables);
        if (!contentType.IsSuccess)
        {
            return Result<ResolvedBody>.Failure(contentType.Error!);
        }

        var content = Encoding.UTF8.GetBytes(text.Value!);
        var type = contentType.Value!.Length > 0 ? contentType.Value : null;
        return Result<ResolvedBody>.Success(new ResolvedBody(content, type));
    }

    private static Result<ResolvedBody> ResolveForm(FormUrlEncodedBody form, IReadOnlyDictionary<string, string> variables)
    {
        var fields = KeyValueResolver.Resolve(form.Fields, variables);
        if (!fields.IsSuccess)
        {
            return Result<ResolvedBody>.Failure(fields.Error!);
        }

        var content = Encoding.UTF8.GetBytes(KeyValueEncoder.Encode(fields.Value!));
        return Result<ResolvedBody>.Success(new ResolvedBody(content, FormUrlEncodedContentType));
    }

    private async Task<Result<ResolvedBody>> ResolveMultipartAsync(
        MultipartBody multipart,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var boundary = $"{MultipartBoundaryPrefix}{_ids.NewId():N}";
        var content = await _multipart.EncodeAsync(multipart.Parts, variables, boundary, cancellationToken);
        if (!content.IsSuccess)
        {
            return Result<ResolvedBody>.Failure(content.Error!);
        }

        return Result<ResolvedBody>.Success(new ResolvedBody(content.Value!, $"{MultipartContentTypePrefix}{boundary}"));
    }
}
