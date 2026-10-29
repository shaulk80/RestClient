using System.Security.Cryptography;
using RestDraft.Application.Abstractions;

namespace RestDraft.Infrastructure.Services;

public sealed class SecureRandomBytes : IRandomBytes
{
    public byte[] Generate(int count)
    {
        return RandomNumberGenerator.GetBytes(count);
    }
}
