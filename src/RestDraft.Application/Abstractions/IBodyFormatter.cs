using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

public interface IBodyFormatter
{
    Result<string> Format(string text);
}
