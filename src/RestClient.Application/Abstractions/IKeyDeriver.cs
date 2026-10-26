namespace RestClient.Application.Abstractions;

public interface IKeyDeriver
{
    byte[] Derive(string password, ReadOnlyMemory<byte> salt, int iterations);
}
