namespace RestClient.Infrastructure.Persistence.Dto;

public sealed record BodyDto(
    BodyKind Kind,
    string ContentType,
    string Text,
    IReadOnlyList<KeyValueDto> Fields,
    IReadOnlyList<MultipartPartDto> Parts);
