using System.Security.Cryptography;
using System.Text;
using RestDraft.Application.Abstractions;

namespace RestDraft.Infrastructure.Security;

// PBKDF2-HMAC-SHA256 producing a 256-bit AES key.
public sealed class Pbkdf2KeyDeriver : IKeyDeriver
{
    private const int KeySize = 32;

    public byte[] Derive(string password, ReadOnlyMemory<byte> salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt.Span, iterations, HashAlgorithmName.SHA256, KeySize);
    }
}
