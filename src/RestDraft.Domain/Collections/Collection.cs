namespace RestDraft.Domain.Collections;

public sealed record Collection(Guid Id, string Name, Folder Root);
