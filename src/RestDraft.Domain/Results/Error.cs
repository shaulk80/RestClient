namespace RestDraft.Domain.Results;

public sealed record Error(ErrorCategory Category, string Message, string? Details = null);
