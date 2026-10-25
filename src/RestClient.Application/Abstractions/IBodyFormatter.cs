using RestClient.Domain.Results;

namespace RestClient.Application.Abstractions;

public interface IBodyFormatter
{
    Result<string> Format(string text);
}
