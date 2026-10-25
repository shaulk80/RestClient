using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;

namespace RestClient.Application.Requests;

// One send, successful or not. ResolvedUrl is the URL as sent, or as typed when the send failed before it was built.
public sealed record RequestAttempt(
    RequestDefinition Request,
    string ResolvedUrl,
    DateTimeOffset SentAt,
    double DurationMs,
    Result<ResponseSnapshot> Outcome);
