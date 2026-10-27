using System.Diagnostics;
using RestClient.Application.Abstractions;
using RestClient.Application.History;
using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;
using RestClient.Domain.Validation;

namespace RestClient.Application.Requests;

public sealed class RequestSender : IRequestSender
{
    private readonly RequestBuilder _builder;
    private readonly IHttpExecutor _executor;
    private readonly IHistoryService _history;
    private readonly IClock _clock;
    private readonly IRequestVariables _variables;

    public RequestSender(
        RequestBuilder builder,
        IHttpExecutor executor,
        IHistoryService history,
        IClock clock,
        IRequestVariables variables)
    {
        _builder = builder;
        _executor = executor;
        _history = history;
        _clock = clock;
        _variables = variables;
    }

    // Every attempt is recorded, including validation failures, locked secrets, network failures and cancellations.
    // A failed history write does not change the result of the send. The history repository logs it.
    public async Task<Result<ResponseSnapshot>> SendAsync(RequestDefinition definition, CancellationToken cancellationToken)
    {
        var sentAt = _clock.UtcNow;
        var timer = Stopwatch.StartNew();
        var prepared = await PrepareAsync(definition, cancellationToken);
        var result = prepared.IsSuccess
            ? await _executor.SendAsync(prepared.Value!, cancellationToken)
            : Result<ResponseSnapshot>.Failure(prepared.Error!);

        var attempt = new RequestAttempt(definition, HistoryUrl.For(definition), sentAt, timer.Elapsed.TotalMilliseconds, result);
        await _history.RecordAsync(attempt, CancellationToken.None);
        return result;
    }

    private async Task<Result<OutgoingRequest>> PrepareAsync(RequestDefinition definition, CancellationToken cancellationToken)
    {
        var validation = RequestValidator.Validate(definition);
        if (!validation.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(validation.Error!);
        }

        var variables = await _variables.ResolveAsync(definition, cancellationToken);
        if (!variables.IsSuccess)
        {
            return Result<OutgoingRequest>.Failure(variables.Error!);
        }

        var built = await _builder.BuildAsync(definition, variables.Value!, cancellationToken);
        if (!built.IsSuccess)
        {
            return built;
        }

        var urlValidation = RequestValidator.ValidateUrl(built.Value!.Url);
        return urlValidation.IsSuccess
            ? built
            : Result<OutgoingRequest>.Failure(urlValidation.Error!);
    }
}
