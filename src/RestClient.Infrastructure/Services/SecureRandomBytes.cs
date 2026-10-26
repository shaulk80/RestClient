using System.Security.Cryptography;
using RestClient.Application.Abstractions;

namespace RestClient.Infrastructure.Services;

public sealed class SecureRandomBytes : IRandomBytes
{
    public byte[] Generate(int count)
    {
        return RandomNumberGenerator.GetBytes(count);
    }
}
