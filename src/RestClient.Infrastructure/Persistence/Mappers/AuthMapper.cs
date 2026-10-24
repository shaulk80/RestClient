using RestClient.Domain.Auth;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence.Dto;

namespace RestClient.Infrastructure.Persistence.Mappers;

public static class AuthMapper
{
    public static AuthDto ToDto(AuthDefinition auth)
    {
        return auth switch
        {
            BasicAuth basic => new AuthDto(AuthKind.Basic, basic.Username, basic.Password, string.Empty, string.Empty, string.Empty, ApiKeyLocation.Header),
            BearerAuth bearer => new AuthDto(AuthKind.Bearer, string.Empty, string.Empty, bearer.Token, string.Empty, string.Empty, ApiKeyLocation.Header),
            ApiKeyAuth apiKey => new AuthDto(AuthKind.ApiKey, string.Empty, string.Empty, string.Empty, apiKey.Key, apiKey.Value, apiKey.Location),
            _ => new AuthDto(AuthKind.None, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, ApiKeyLocation.Header),
        };
    }

    public static Result<AuthDefinition> ToDomain(AuthDto dto)
    {
        return dto.Kind switch
        {
            AuthKind.None => Result<AuthDefinition>.Success(new NoAuth()),
            AuthKind.Basic => Result<AuthDefinition>.Success(new BasicAuth(dto.Username, dto.Password)),
            AuthKind.Bearer => Result<AuthDefinition>.Success(new BearerAuth(dto.Token)),
            AuthKind.ApiKey => Result<AuthDefinition>.Success(new ApiKeyAuth(dto.Key, dto.Value, dto.Location)),
            _ => Result<AuthDefinition>.Failure(new Error(ErrorCategory.Storage, "A stored request auth has an unknown type.")),
        };
    }
}
