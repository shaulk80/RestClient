using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Domain.Validation;

public static class RequestValidator
{
    public static Result Validate(RequestDefinition definition)
    {
        if (!Enum.IsDefined(definition.Method))
        {
            return Invalid("Choose a valid HTTP method.");
        }

        if (HasEmptyEnabledKey(definition.QueryParams))
        {
            return Invalid("Query parameter names must not be empty.");
        }

        if (HasEmptyEnabledKey(definition.Headers))
        {
            return Invalid("Header names must not be empty.");
        }

        return definition.Body switch
        {
            FormUrlEncodedBody form when HasEmptyEnabledKey(form.Fields) => Invalid("Form field names must not be empty."),
            MultipartBody multipart => ValidateParts(multipart.Parts),
            _ => Result.Success(),
        };
    }

    public static Result ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsHttpScheme(uri))
        {
            return Invalid("The URL must be an absolute http or https URL.");
        }

        return Result.Success();
    }

    private static bool HasEmptyEnabledKey(IEnumerable<KeyValueEntry> entries)
    {
        return entries.Any(entry => entry.IsEnabled && string.IsNullOrWhiteSpace(entry.Key));
    }

    private static Result ValidateParts(IEnumerable<MultipartPart> parts)
    {
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part.Name))
            {
                return Invalid("Multipart part names must not be empty.");
            }

            if ((part.Value is null) == (part.FilePath is null))
            {
                return Invalid($"Multipart part '{part.Name}' needs either a value or a file path, not both or neither.");
            }

            if (part.FilePath is not null && string.IsNullOrWhiteSpace(part.FilePath))
            {
                return Invalid($"Multipart part '{part.Name}' needs a file path.");
            }
        }

        return Result.Success();
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private static Result Invalid(string message)
    {
        return Result.Failure(new Error(ErrorCategory.Validation, message));
    }
}
