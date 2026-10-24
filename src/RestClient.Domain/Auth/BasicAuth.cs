namespace RestClient.Domain.Auth;

public sealed record BasicAuth(string Username, string Password) : AuthDefinition;
