namespace RestDraft.Domain.Auth;

public sealed record ApiKeyAuth(string Key, string Value, ApiKeyLocation Location) : AuthDefinition;
