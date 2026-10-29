namespace RestDraft.Domain.Results;

public sealed class Result
{
    private Result(Error? error)
    {
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public Error? Error { get; }

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);
}
