using RestClient.Domain.Results;

namespace RestClient.Domain.Environments;

public static class VariableNameRules
{
    public static Result ValidateName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return Invalid("Variable names must not be empty.");
        }

        if (trimmed.IndexOfAny(['{', '}']) >= 0)
        {
            return Invalid($"The variable name '{trimmed}' must not contain braces.");
        }

        return Result.Success();
    }

    public static Result ValidateUnique(IEnumerable<Variable> variables)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variable in variables)
        {
            if (!names.Add(variable.Name.Trim()))
            {
                return Invalid($"The variable '{variable.Name.Trim()}' is defined more than once.");
            }
        }

        return Result.Success();
    }

    private static Result Invalid(string message)
    {
        return Result.Failure(ResultErrors.Validation(message));
    }
}
