namespace RestClient.Infrastructure.Persistence.Dto;

public sealed record CollectionDto(Guid Id, string Name, FolderDto Root);
