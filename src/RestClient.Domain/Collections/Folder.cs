namespace RestClient.Domain.Collections;

public sealed record Folder(Guid Id, string Name, IReadOnlyList<Folder> Folders, IReadOnlyList<RequestDefinition> Requests);
