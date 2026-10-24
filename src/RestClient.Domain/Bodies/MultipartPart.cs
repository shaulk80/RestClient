namespace RestClient.Domain.Bodies;

public sealed record MultipartPart(string Name, string? Value, string? FilePath);
