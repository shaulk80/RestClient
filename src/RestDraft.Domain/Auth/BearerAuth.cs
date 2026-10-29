namespace RestDraft.Domain.Auth;

public sealed record BearerAuth(string Token) : AuthDefinition;
