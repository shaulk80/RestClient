using System.Text;
using RestDraft.Domain.Auth;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Requests;

public static class AuthResolver
{
    private const string AuthorizationHeader = "Authorization";

    public static Result<ResolvedAuth> Resolve(AuthDefinition auth, IReadOnlyDictionary<string, string> variables)
    {
        return auth switch
        {
            BasicAuth basic => ResolveBasic(basic, variables),
            BearerAuth bearer => ResolveBearer(bearer, variables),
            ApiKeyAuth apiKey => ResolveApiKey(apiKey, variables),
            _ => Result<ResolvedAuth>.Success(ResolvedAuth.None),
        };
    }

    private static Result<ResolvedAuth> ResolveBasic(BasicAuth basic, IReadOnlyDictionary<string, string> variables)
    {
        var username = VariableResolver.Resolve(basic.Username, variables);
        if (!username.IsSuccess)
        {
            return Result<ResolvedAuth>.Failure(username.Error!);
        }

        var password = VariableResolver.Resolve(basic.Password, variables);
        if (!password.IsSuccess)
        {
            return Result<ResolvedAuth>.Failure(password.Error!);
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username.Value}:{password.Value}"));
        return HeaderAuth($"Basic {credentials}");
    }

    private static Result<ResolvedAuth> ResolveBearer(BearerAuth bearer, IReadOnlyDictionary<string, string> variables)
    {
        var token = VariableResolver.Resolve(bearer.Token, variables);
        return token.IsSuccess
            ? HeaderAuth($"Bearer {token.Value}")
            : Result<ResolvedAuth>.Failure(token.Error!);
    }

    private static Result<ResolvedAuth> ResolveApiKey(ApiKeyAuth apiKey, IReadOnlyDictionary<string, string> variables)
    {
        var key = VariableResolver.Resolve(apiKey.Key, variables);
        if (!key.IsSuccess)
        {
            return Result<ResolvedAuth>.Failure(key.Error!);
        }

        var value = VariableResolver.Resolve(apiKey.Value, variables);
        if (!value.IsSuccess)
        {
            return Result<ResolvedAuth>.Failure(value.Error!);
        }

        var entry = new KeyValueEntry(key.Value!, value.Value!, true);
        return apiKey.Location == ApiKeyLocation.Query
            ? Result<ResolvedAuth>.Success(new ResolvedAuth([], [entry]))
            : Result<ResolvedAuth>.Success(new ResolvedAuth([entry], []));
    }

    private static Result<ResolvedAuth> HeaderAuth(string authorization)
    {
        var header = new KeyValueEntry(AuthorizationHeader, authorization, true);
        return Result<ResolvedAuth>.Success(new ResolvedAuth([header], []));
    }
}
