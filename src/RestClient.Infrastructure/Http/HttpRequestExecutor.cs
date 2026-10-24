using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RestClient.Application.Abstractions;
using RestClient.Application.Requests;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;

namespace RestClient.Infrastructure.Http;

public sealed class HttpRequestExecutor : IHttpExecutor
{
    private static readonly Action<ILogger, string, Exception?> RequestFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1, nameof(RequestFailed)),
        "HTTP request failed: {Reason}");

    private static readonly Action<ILogger, string, Exception?> RequestFailedUnexpectedly = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(2, nameof(RequestFailedUnexpectedly)),
        "HTTP request failed unexpectedly: {Reason}");

    private readonly ResponseBodyReader _bodyReader;
    private readonly HttpTransportOptions _options;
    private readonly ILogger<HttpRequestExecutor> _logger;

    public HttpRequestExecutor(
        ResponseBodyReader bodyReader,
        HttpTransportOptions options,
        ILogger<HttpRequestExecutor> logger)
    {
        _bodyReader = bodyReader;
        _options = options;
        _logger = logger;
    }

    public async Task<Result<ResponseSnapshot>> SendAsync(OutgoingRequest request, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.Timeout);

        var recorder = new ConnectTimingRecorder();
        using var client = RequestHttpClientFactory.Create(_options, recorder);
        try
        {
            return await ExecuteAsync(client, request, recorder, deadline.Token);
        }
        catch (OperationCanceledException exception)
        {
            return cancellationToken.IsCancellationRequested
                ? Fail(ErrorCategory.Cancelled, "The request was cancelled.", exception)
                : Fail(ErrorCategory.Timeout, "The request timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            return Fail(ErrorCategory.Network, "The request could not reach the server.", exception);
        }
        catch (Exception exception)
        {
            return Fail(ErrorCategory.Unexpected, "The request failed unexpectedly.", exception);
        }
    }

    private async Task<Result<ResponseSnapshot>> ExecuteAsync(
        HttpClient client,
        OutgoingRequest request,
        ConnectTimingRecorder recorder,
        CancellationToken cancellationToken)
    {
        using var message = HttpRequestMessageFactory.Create(request);
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var timeToFirstByte = stopwatch.Elapsed;

        var body = await _bodyReader.ReadAsync(response.Content, cancellationToken);
        if (!body.IsSuccess)
        {
            return Result<ResponseSnapshot>.Failure(body.Error!);
        }

        var timing = new ResponseTiming(
            recorder.ConnectDuration?.TotalMilliseconds,
            timeToFirstByte.TotalMilliseconds,
            stopwatch.Elapsed.TotalMilliseconds);
        return Result<ResponseSnapshot>.Success(ResponseSnapshotFactory.Create(response, body.Value!, timing));
    }

    private Result<ResponseSnapshot> Fail(ErrorCategory category, string message, Exception exception)
    {
        if (category == ErrorCategory.Unexpected)
        {
            RequestFailedUnexpectedly(_logger, message, exception);
        }
        else
        {
            RequestFailed(_logger, message, exception);
        }

        return Result<ResponseSnapshot>.Failure(new Error(category, message, exception.Message));
    }
}
