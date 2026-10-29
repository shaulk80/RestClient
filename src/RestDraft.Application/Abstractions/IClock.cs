namespace RestDraft.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
