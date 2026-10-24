using RestClient.Application.Abstractions;
using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;
using RestClient.Domain.Validation;

namespace RestClient.Application.Requests;

public sealed class RequestSender : IRequestSender
{
    private readonly RequestBuilder _builder;
    private readonly IHttpExecutor _executor;

    public RequestSender(RequestBuilder builder, IHttpExecutor executor)
    {
        _builder = builder;
        _executor = executor;
    }

    public async Task<Result<ResponseSnapshot>> SendAsync(
        RequestDefinition definition,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var validation = RequestValidator.Validate(definition);
        if (!validation.IsSuccess)
        {
            return Result<ResponseSnapshot>.Failure(validation.Error!);
        }

        var built = _builder.Build(definition, variables);
        if (!built.IsSuccess)
        {
            return Result<ResponseSnapshot>.Failure(built.Error!);
        }

        var urlValidation = RequestValidator.ValidateUrl(built.Value!.Url);
        if (!urlValidation.IsSuccess)
        {
            return Result<ResponseSnapshot>.Failure(urlValidation.Error!);
        }

        return await _executor.SendAsync(built.Value, cancellationToken);
    }
}
