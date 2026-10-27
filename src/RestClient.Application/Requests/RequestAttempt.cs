using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;

namespace RestClient.Application.Requests;

// One send, successful or not. Url is the URL as typed (see HistoryUrl), never the resolved one, so secret values stay out of history.
public sealed record RequestAttempt(
    RequestDefinition Request,
    string Url,
    DateTimeOffset SentAt,
    double DurationMs,
    Result<ResponseSnapshot> Outcome);
