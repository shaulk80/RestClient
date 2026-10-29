using RestDraft.Application.Abstractions;
using RestDraft.Application.Requests;
using RestDraft.Application.Secrets;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Environments;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Environments;

// Values come from the active environment. A secret is decrypted only when the request refers to it,
// so a locked secret blocks only the requests that need it.
public sealed class EnvironmentRequestVariables : IRequestVariables
{
    private readonly IEnvironmentService _environments;
    private readonly ISecretService _secrets;

    public EnvironmentRequestVariables(IEnvironmentService environments, ISecretService secrets)
    {
        _environments = environments;
        _secrets = secrets;
    }

    public async Task<Result<IReadOnlyDictionary<string, string>>> ResolveAsync(
        RequestDefinition definition,
        CancellationToken cancellationToken)
    {
        var active = await _environments.GetActiveAsync(cancellationToken);
        if (!active.IsSuccess)
        {
            return Result<IReadOnlyDictionary<string, string>>.Failure(active.Error!);
        }

        if (active.Value is not { } environment)
        {
            return Result<IReadOnlyDictionary<string, string>>.Success(new Dictionary<string, string>());
        }

        return ResolveVariables(environment, RequestPlaceholders.NamesIn(definition));
    }

    private Result<IReadOnlyDictionary<string, string>> ResolveVariables(RequestEnvironment environment, IReadOnlySet<string> names)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variable in environment.Variables)
        {
            var value = ValueOf(variable, names);
            if (!value.IsSuccess)
            {
                return Result<IReadOnlyDictionary<string, string>>.Failure(value.Error!);
            }

            if (value.Value is not null)
            {
                values[variable.Name] = value.Value;
            }
        }

        return Result<IReadOnlyDictionary<string, string>>.Success(values);
    }

    // Returns null for a secret that the request does not use, so it is never decrypted.
    private Result<string?> ValueOf(Variable variable, IReadOnlySet<string> names)
    {
        if (!variable.IsSecret)
        {
            return Result<string?>.Success(variable.Value);
        }

        if (!names.Contains(variable.Name))
        {
            return Result<string?>.Success(null);
        }

        if (!_secrets.IsUnlocked)
        {
            return Result<string?>.Failure(new Error(
                ErrorCategory.Security,
                $"Unlock the secrets to use the variable '{variable.Name}'."));
        }

        var decrypted = _secrets.Decrypt(variable.Value);
        return decrypted.IsSuccess
            ? Result<string?>.Success(decrypted.Value)
            : Result<string?>.Failure(decrypted.Error!);
    }
}
