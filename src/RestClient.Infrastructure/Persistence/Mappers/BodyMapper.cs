using RestClient.Domain.Bodies;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence.Dto;

namespace RestClient.Infrastructure.Persistence.Mappers;

public static class BodyMapper
{
    public static BodyDto ToDto(BodyDefinition body)
    {
        return body switch
        {
            RawBody raw => new BodyDto(BodyKind.Raw, raw.ContentType, raw.Text, [], []),
            FormUrlEncodedBody form => new BodyDto(BodyKind.FormUrlEncoded, string.Empty, string.Empty, KeyValueMapper.ToDtos(form.Fields), []),
            MultipartBody multipart => new BodyDto(BodyKind.Multipart, string.Empty, string.Empty, [], ToPartDtos(multipart.Parts)),
            _ => new BodyDto(BodyKind.None, string.Empty, string.Empty, [], []),
        };
    }

    public static Result<BodyDefinition> ToDomain(BodyDto dto)
    {
        return dto.Kind switch
        {
            BodyKind.None => Result<BodyDefinition>.Success(new NoBody()),
            BodyKind.Raw => Result<BodyDefinition>.Success(new RawBody(dto.ContentType, dto.Text)),
            BodyKind.FormUrlEncoded => Result<BodyDefinition>.Success(new FormUrlEncodedBody(KeyValueMapper.ToDomain(dto.Fields))),
            BodyKind.Multipart => Result<BodyDefinition>.Success(new MultipartBody(ToParts(dto.Parts))),
            _ => Result<BodyDefinition>.Failure(new Error(ErrorCategory.Storage, "A stored request body has an unknown type.")),
        };
    }

    private static List<MultipartPartDto> ToPartDtos(IReadOnlyList<MultipartPart> parts)
    {
        return parts.Select(part => new MultipartPartDto(part.Name, part.Value, part.FilePath)).ToList();
    }

    private static List<MultipartPart> ToParts(IReadOnlyList<MultipartPartDto> dtos)
    {
        return dtos.Select(dto => new MultipartPart(dto.Name, dto.Value, dto.FilePath)).ToList();
    }
}
