namespace RestDraft.Domain.Results;

public enum ErrorCategory
{
    Validation,
    NotFound,
    Conflict,
    Network,
    Timeout,
    Cancelled,
    Storage,
    Security,
    Unexpected
}
