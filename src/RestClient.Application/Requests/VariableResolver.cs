using System.Text.RegularExpressions;
using RestClient.Domain.Results;

namespace RestClient.Application.Requests;

public static partial class VariableResolver
{
    public static Result<string> Resolve(string template, IReadOnlyDictionary<string, string> variables)
    {
        var unresolved = new List<string>();
        var resolved = PlaceholderPattern().Replace(template, match =>
        {
            var name = match.Groups[1].Value.Trim();
            if (variables.TryGetValue(name, out var value))
            {
                return value;
            }

            unresolved.Add(match.Value);
            return match.Value;
        });

        if (unresolved.Count > 0)
        {
            return Result<string>.Failure(new Error(
                ErrorCategory.Validation,
                $"Unresolved variables: {string.Join(", ", unresolved)}."));
        }

        return Result<string>.Success(resolved);
    }

    public static IReadOnlySet<string> PlaceholderNames(string template)
    {
        return PlaceholderPattern()
            .Matches(template)
            .Select(match => match.Groups[1].Value.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    [GeneratedRegex(@"\{\{([^{}]*)\}\}")]
    private static partial Regex PlaceholderPattern();
}
