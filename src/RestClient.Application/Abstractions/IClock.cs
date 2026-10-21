namespace RestClient.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
