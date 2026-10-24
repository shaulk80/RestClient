namespace RestClient.Infrastructure.Persistence.Dto;

// Children are added after the folder is created, so the reader builds the tree bottom-up by linking parents to children.
public sealed class FolderDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public List<FolderDto> Folders { get; } = [];

    public List<RequestDto> Requests { get; } = [];
}
