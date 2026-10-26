namespace RestClient.Application.Abstractions;

public interface IRandomBytes
{
    byte[] Generate(int count);
}
