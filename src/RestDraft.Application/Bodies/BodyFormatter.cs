using RestDraft.Application.Abstractions;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Bodies;

public abstract class BodyFormatter : IBodyFormatter
{
    public Result<string> Format(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<string>.Failure(ResultErrors.Validation("There is no body to format."));
        }

        return Reformat(text);
    }

    protected abstract Result<string> Reformat(string text);
}
