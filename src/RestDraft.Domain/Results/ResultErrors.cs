namespace RestDraft.Domain.Results;

public static class ResultErrors
{
    public static Error NotFound(string itemName)
    {
        return new Error(ErrorCategory.NotFound, $"{itemName} was not found.");
    }

    public static Error Validation(string message)
    {
        return new Error(ErrorCategory.Validation, message);
    }
}
