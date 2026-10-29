using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;
using RestDraft.Infrastructure.Persistence.Dto;

namespace RestDraft.Infrastructure.Persistence.Mappers;

public static class RequestMapper
{
    public static RequestDto ToDto(RequestDefinition request)
    {
        return new RequestDto(
            request.Id,
            request.Name,
            request.Method,
            request.Url,
            KeyValueMapper.ToDtos(request.QueryParams),
            KeyValueMapper.ToDtos(request.Headers),
            BodyMapper.ToDto(request.Body),
            AuthMapper.ToDto(request.Auth));
    }

    public static Result<RequestDefinition> ToDomain(RequestDto dto)
    {
        var body = BodyMapper.ToDomain(dto.Body);
        if (!body.IsSuccess)
        {
            return Result<RequestDefinition>.Failure(body.Error!);
        }

        var auth = AuthMapper.ToDomain(dto.Auth);
        if (!auth.IsSuccess)
        {
            return Result<RequestDefinition>.Failure(auth.Error!);
        }

        return Result<RequestDefinition>.Success(new RequestDefinition
        {
            Id = dto.Id,
            Name = dto.Name,
            Method = dto.Method,
            Url = dto.Url,
            QueryParams = KeyValueMapper.ToDomain(dto.QueryParams),
            Headers = KeyValueMapper.ToDomain(dto.Headers),
            Body = body.Value!,
            Auth = auth.Value!,
        });
    }
}
