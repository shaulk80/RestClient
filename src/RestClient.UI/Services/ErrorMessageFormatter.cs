using RestClient.Domain.Results;

namespace RestClient.UI.Services;

public static class ErrorMessageFormatter
{
    public static string Format(Error error)
    {
        return string.IsNullOrWhiteSpace(error.Details)
            ? error.Message
            : $"{error.Message} ({error.Details})";
    }
}
