using RestClient.Domain.Collections;

namespace RestClient.Infrastructure.Persistence.Dto;

public sealed record RequestDto(
    Guid Id,
    string Name,
    RequestMethod Method,
    string Url,
    IReadOnlyList<KeyValueDto> QueryParams,
    IReadOnlyList<KeyValueDto> Headers,
    BodyDto Body,
    AuthDto Auth);
