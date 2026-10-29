namespace RestDraft.Domain.Bodies;

public sealed record MultipartBody(IReadOnlyList<MultipartPart> Parts) : BodyDefinition;
