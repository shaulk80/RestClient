using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.Infrastructure.Persistence.Dto;

namespace RestClient.Infrastructure.Persistence.Mappers;

public static class CollectionMapper
{
    public static CollectionDto ToDto(Collection collection)
    {
        return new CollectionDto(collection.Id, collection.Name, FolderToDto(collection.Root));
    }

    public static Result<IReadOnlyList<Collection>> ToDomain(IReadOnlyList<CollectionDto> dtos)
    {
        var collections = new List<Collection>();
        foreach (var dto in dtos)
        {
            var root = FolderToDomain(dto.Root);
            if (!root.IsSuccess)
            {
                return Result<IReadOnlyList<Collection>>.Failure(root.Error!);
            }

            collections.Add(new Collection(dto.Id, dto.Name, root.Value!));
        }

        return Result<IReadOnlyList<Collection>>.Success(collections);
    }

    private static FolderDto FolderToDto(Folder folder)
    {
        var dto = new FolderDto
        {
            Id = folder.Id,
            Name = folder.Name,
        };
        dto.Folders.AddRange(folder.Folders.Select(FolderToDto));
        dto.Requests.AddRange(folder.Requests.Select(RequestMapper.ToDto));
        return dto;
    }

    private static Result<Folder> FolderToDomain(FolderDto dto)
    {
        var folders = new List<Folder>();
        foreach (var child in dto.Folders)
        {
            var folder = FolderToDomain(child);
            if (!folder.IsSuccess)
            {
                return folder;
            }

            folders.Add(folder.Value!);
        }

        var requests = new List<RequestDefinition>();
        foreach (var requestDto in dto.Requests)
        {
            var request = RequestMapper.ToDomain(requestDto);
            if (!request.IsSuccess)
            {
                return Result<Folder>.Failure(request.Error!);
            }

            requests.Add(request.Value!);
        }

        return Result<Folder>.Success(new Folder(dto.Id, dto.Name, folders, requests));
    }
}
