namespace RestDraft.Application.Requests;

// ContentType is null when the body does not set one, so the headers the user wrote stay as they are.
public sealed record ResolvedBody(ReadOnlyMemory<byte>? Content, string? ContentType)
{
    public static ResolvedBody Empty { get; } = new(null, null);
}
