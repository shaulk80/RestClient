namespace RestClient.Domain.Bodies;

public sealed record RawBody(string ContentType, string Text) : BodyDefinition;
