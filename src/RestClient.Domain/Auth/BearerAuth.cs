namespace RestClient.Domain.Auth;

public sealed record BearerAuth(string Token) : AuthDefinition;
