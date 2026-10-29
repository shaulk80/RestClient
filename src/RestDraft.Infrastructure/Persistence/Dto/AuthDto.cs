using RestDraft.Domain.Auth;

namespace RestDraft.Infrastructure.Persistence.Dto;

public sealed record AuthDto(
    AuthKind Kind,
    string Username,
    string Password,
    string Token,
    string Key,
    string Value,
    ApiKeyLocation Location);
