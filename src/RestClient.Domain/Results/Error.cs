namespace RestClient.Domain.Results;

public sealed record Error(ErrorCategory Category, string Message, string? Details = null);
